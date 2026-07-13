using System.Runtime.ExceptionServices;
using FFDecsaSharp.CSA;
using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.Gui.Services;

/// <summary>
/// Keeps a fixed set of dedicated packet-decryption workers alive for one file operation or benchmark run.
/// </summary>
internal sealed class PacketDecryptionWorkerGroup : IDisposable
{
    private readonly Decryptor _decryptor;
    private readonly int _workerCount;
    private readonly Barrier? _barrier;
    private readonly Task[] _workers;
    private readonly object _requestGate = new();
    private byte[]? _packets;
    private PacketDecryptionResult[]? _results;
    private int _packetCount;
    private CancellationToken _cancellationToken;
    private ExceptionDispatchInfo? _failure;
    private int _isDisposed;

    public PacketDecryptionWorkerGroup(Decryptor decryptor, int requestedWorkerCount)
    {
        _decryptor = decryptor ?? throw new ArgumentNullException(nameof(decryptor));
        _workerCount = AppSettingsService.CoerceDecryptionWorkerCount(requestedWorkerCount);
        if (_workerCount == 1)
        {
            _workers = [];
            return;
        }

        _barrier = new Barrier(_workerCount + 1);
        _workers = new Task[_workerCount];
        for (int workerIndex = 0; workerIndex < _workers.Length; workerIndex++)
        {
            int capturedWorkerIndex = workerIndex;
            _workers[workerIndex] = Task.Factory.StartNew(
                () => RunWorker(capturedWorkerIndex),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    public bool TryDecrypt(
        byte[] packets,
        PacketDecryptionResult[] results,
        int packetCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(results);
        ThrowIfDisposed();
        if (packetCount < 0
            || packets.Length < checked(packetCount * TransportPacket.Size)
            || results.Length < packetCount)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_workerCount == 1)
        {
            return _decryptor.TryDecryptPackets(
                packets.AsSpan(0, packetCount * TransportPacket.Size),
                results.AsSpan(0, packetCount));
        }

        lock (_requestGate)
        {
            _packets = packets;
            _results = results;
            _packetCount = packetCount;
            _cancellationToken = cancellationToken;
            Volatile.Write(ref _failure, null);

            _barrier!.SignalAndWait();
            _barrier.SignalAndWait();

            ExceptionDispatchInfo? failure = Volatile.Read(ref _failure);
            if (failure is not null)
            {
                failure.Throw();
            }

            return true;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        if (_workerCount == 1)
        {
            return;
        }

        lock (_requestGate)
        {
            _barrier!.SignalAndWait();
        }

        Task.WaitAll(_workers);
        _barrier!.Dispose();
    }

    private void RunWorker(int workerIndex)
    {
        while (true)
        {
            _barrier!.SignalAndWait();
            if (Volatile.Read(ref _isDisposed) != 0)
            {
                return;
            }

            try
            {
                DecryptPartition(workerIndex);
            }
            catch (Exception exception)
            {
                Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(exception), null);
            }

            _barrier.SignalAndWait();
        }
    }

    private void DecryptPartition(int workerIndex)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        int batchCount = (_packetCount + PacketBlockDecryptionService.PacketsPerBitsliceBatch - 1) / PacketBlockDecryptionService.PacketsPerBitsliceBatch;
        int firstBatch = (workerIndex * batchCount) / _workerCount;
        int lastBatch = ((workerIndex + 1) * batchCount) / _workerCount;
        int firstPacket = firstBatch * PacketBlockDecryptionService.PacketsPerBitsliceBatch;
        int lastPacket = Math.Min(_packetCount, lastBatch * PacketBlockDecryptionService.PacketsPerBitsliceBatch);
        int partitionPacketCount = lastPacket - firstPacket;
        if (partitionPacketCount == 0)
        {
            return;
        }

        if (!_decryptor.TryDecryptPackets(
            _packets!.AsSpan(firstPacket * TransportPacket.Size, partitionPacketCount * TransportPacket.Size),
            _results!.AsSpan(firstPacket, partitionPacketCount)))
        {
            throw new InvalidOperationException("Packet partition decryption failed.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            throw new ObjectDisposedException(nameof(PacketDecryptionWorkerGroup));
        }
    }
}
