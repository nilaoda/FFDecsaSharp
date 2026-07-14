using System.Diagnostics;
using System.Runtime.ExceptionServices;
using FFDecsaSharp.CSA;

namespace FFDecsaSharp.TransportStream;

/// <summary>Reports the progress of a transport-stream decryption operation.</summary>
public readonly record struct TransportStreamDecryptionProgress(
    long ProcessedBytes,
    long TotalBytes,
    long PacketCount,
    long DecryptedCount,
    TimeSpan Elapsed);

/// <summary>Describes a completed transport-stream decryption operation.</summary>
public readonly record struct TransportStreamDecryptionSummary(
    long PacketCount,
    long DecryptedCount,
    long SkippedLeadingBytes,
    long IgnoredTrailingBytes,
    TimeSpan Elapsed);

/// <summary>
/// Decrypts one or more MPEG transport-stream files without any UI or application-settings dependency.
/// </summary>
public static class TransportStreamDecryptionService
{
    /// <summary>The baseline number of packets read and written per block.</summary>
    public const int DefaultPacketsPerBlock = 4096;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Decrypts the requested packet range from one or more input files into an output file.</summary>
    public static async Task<TransportStreamDecryptionSummary> DecryptAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        ReadOnlyMemory<byte> evenKey,
        ReadOnlyMemory<byte> oddKey,
        long packetOffset,
        long packetLimit,
        int workerCount,
        IProgress<TransportStreamDecryptionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (packetOffset < 0) throw new ArgumentOutOfRangeException(nameof(packetOffset));
        if (packetLimit < 0) throw new ArgumentOutOfRangeException(nameof(packetLimit));
        if (!ControlWords.TryCreate(evenKey.Span, oddKey.Span, out var words) || !Decryptor.TryCreate(words, out var decryptor))
            throw new InvalidDataException("Control words must each contain eight valid bytes.");
        if (inputPaths.Count == 0) throw new InvalidDataException("At least one input file is required.");

        IReadOnlyList<PreparedInput> preparedInputs = await PrepareInputsAsync(inputPaths, packetOffset, packetLimit, cancellationToken).ConfigureAwait(false);
        long totalBytes = preparedInputs.Sum(static input => input.Length);
        long skippedLeadingBytes = preparedInputs.Sum(static input => input.SkippedLeadingBytes);
        long ignoredTrailingBytes = preparedInputs.Sum(static input => input.IgnoredTrailingBytes);
        if (totalBytes == 0) throw new InvalidDataException("No aligned transport-stream packets were found.");

        EnsureSufficientDiskSpace(outputPath, totalBytes);
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        int effectiveWorkerCount = CoerceWorkerCount(workerCount);
        int packetsPerBlock = GetPacketsPerBlock(effectiveWorkerCount);
        await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[TransportPacket.Size * packetsPerBlock];
        var results = new PacketDecryptionResult[packetsPerBlock];
        using var workerGroup = new PacketDecryptionWorkerGroup(decryptor!, effectiveWorkerCount);
        long processed = 0;
        long packets = 0;
        long decrypted = 0;
        var started = Stopwatch.StartNew();
        TimeSpan lastProgressElapsed = TimeSpan.Zero;

        foreach (PreparedInput prepared in preparedInputs)
        {
            await using var input = new FileStream(prepared.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            input.Position = prepared.StartPosition;
            long remaining = prepared.Length;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int blockLength = (int)Math.Min(buffer.Length, remaining);
                await input.ReadExactlyAsync(buffer.AsMemory(0, blockLength), cancellationToken).ConfigureAwait(false);
                int blockPackets = blockLength / TransportPacket.Size;
                if (!workerGroup.TryDecrypt(buffer, results, blockPackets, cancellationToken))
                    throw new InvalidDataException("Packet decryption failed.");
                await output.WriteAsync(buffer.AsMemory(0, blockLength), cancellationToken).ConfigureAwait(false);

                processed += blockLength;
                remaining -= blockLength;
                packets += blockPackets;
                for (int packetIndex = 0; packetIndex < blockPackets; packetIndex++)
                    if (results[packetIndex] == PacketDecryptionResult.Decrypted) decrypted++;

                TimeSpan elapsed = started.Elapsed;
                if (processed == totalBytes || elapsed - lastProgressElapsed >= ProgressInterval)
                {
                    progress?.Report(new TransportStreamDecryptionProgress(processed, totalBytes, packets, decrypted, elapsed));
                    lastProgressElapsed = elapsed;
                }
            }
        }

        return new TransportStreamDecryptionSummary(packets, decrypted, skippedLeadingBytes, ignoredTrailingBytes, started.Elapsed);
    }

    /// <summary>Clamps a requested worker count to the processors available on this machine.</summary>
    public static int CoerceWorkerCount(int workerCount) => Math.Clamp(workerCount, 1, Math.Max(1, Environment.ProcessorCount));

    /// <summary>Gets a block size large enough to give every worker useful bitsliced work.</summary>
    public static int GetPacketsPerBlock(int workerCount) => Math.Max(
        DefaultPacketsPerBlock,
        checked(CoerceWorkerCount(workerCount) * DefaultPacketsPerBlock));

    private static async Task<IReadOnlyList<PreparedInput>> PrepareInputsAsync(IReadOnlyList<string> inputPaths, long packetOffset, long packetLimit, CancellationToken cancellationToken)
    {
        long requestedOffset = checked(packetOffset * TransportPacket.Size);
        long remainingLimit = packetLimit == 0 ? long.MaxValue : checked(packetLimit * TransportPacket.Size);
        var prepared = new List<PreparedInput>(inputPaths.Count);

        foreach (string inputPath in inputPaths)
        {
            if (remainingLimit == 0) break;
            await using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            long syncPosition = await FindSyncPositionAsync(input, requestedOffset, cancellationToken).ConfigureAwait(false);
            if (syncPosition < 0) throw new InvalidDataException($"Unable to find MPEG-TS synchronization in '{inputPath}'.");

            long availableBytes = ((input.Length - syncPosition) / TransportPacket.Size) * TransportPacket.Size;
            long length = Math.Min(availableBytes, remainingLimit);
            if (length == 0) continue;

            long ignoredTrailingBytes = packetLimit == 0 ? input.Length - syncPosition - availableBytes : 0;
            long skippedLeadingBytes = syncPosition - requestedOffset;
            prepared.Add(new PreparedInput(inputPath, syncPosition, length, skippedLeadingBytes, ignoredTrailingBytes));
            if (remainingLimit != long.MaxValue) remainingLimit -= length;
        }

        return prepared;
    }

    private static void EnsureSufficientDiskSpace(string outputPath, long requiredBytes)
    {
        string fullOutputPath = Path.GetFullPath(outputPath);
        string existingDirectory = FindExistingDirectory(Path.GetDirectoryName(fullOutputPath) ?? Environment.CurrentDirectory);
        DriveInfo? volume = TryFindVolume(existingDirectory);
        if (volume is not null && (!volume.IsReady || volume.AvailableFreeSpace < requiredBytes))
            throw new IOException("Insufficient free disk space for the decrypted output.");
    }

    private static DriveInfo? TryFindVolume(string existingDirectory)
    {
        try
        {
            DriveInfo? volume = DriveInfo.GetDrives()
                .Where(static drive => drive.IsReady)
                .OrderByDescending(static drive => drive.RootDirectory.FullName.Length)
                .FirstOrDefault(drive => IsPathOnVolume(existingDirectory, drive.RootDirectory.FullName));
            if (volume is not null) return volume;
        }
        catch (ArgumentException)
        {
            // Some Unix environments can expose an invalid mount entry. Fall back to the path root.
        }

        string? root = Path.GetPathRoot(existingDirectory);
        if (string.IsNullOrWhiteSpace(root)) return null;
        try { return new DriveInfo(root); }
        catch (ArgumentException) { return null; }
    }

    private static string FindExistingDirectory(string directory)
    {
        string current = Path.GetFullPath(directory);
        while (!Directory.Exists(current))
        {
            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent is null) break;
            current = parent.FullName;
        }
        return current;
    }

    private static bool IsPathOnVolume(string path, string root) => path.StartsWith(
        Path.GetFullPath(root),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static async Task<long> FindSyncPositionAsync(FileStream input, long offset, CancellationToken cancellationToken)
    {
        if (offset < 0 || offset >= input.Length) return -1;
        input.Position = offset;
        int length = (int)Math.Min(1024 * 1024, input.Length - offset);
        var probe = new byte[length];
        await input.ReadExactlyAsync(probe, cancellationToken).ConfigureAwait(false);

        for (int index = 0; index < probe.Length; index++)
        {
            if (probe[index] != TransportPacket.SyncByte) continue;
            if (IsSyncAt(probe, index + TransportPacket.Size) && IsSyncAt(probe, index + (TransportPacket.Size * 2))) return offset + index;
        }

        return -1;
    }

    private static bool IsSyncAt(byte[] probe, int index) => index >= probe.Length || probe[index] == TransportPacket.SyncByte;

    private readonly record struct PreparedInput(string Path, long StartPosition, long Length, long SkippedLeadingBytes, long IgnoredTrailingBytes);
}

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

    public PacketDecryptionWorkerGroup(Decryptor decryptor, int workerCount)
    {
        _decryptor = decryptor ?? throw new ArgumentNullException(nameof(decryptor));
        _workerCount = TransportStreamDecryptionService.CoerceWorkerCount(workerCount);
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

    public bool TryDecrypt(byte[] packets, PacketDecryptionResult[] results, int packetCount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(results);
        ThrowIfDisposed();
        if (packetCount < 0 || packets.Length < checked(packetCount * TransportPacket.Size) || results.Length < packetCount) return false;

        cancellationToken.ThrowIfCancellationRequested();
        if (_workerCount == 1)
            return _decryptor.TryDecryptPackets(packets.AsSpan(0, packetCount * TransportPacket.Size), results.AsSpan(0, packetCount));

        lock (_requestGate)
        {
            _packets = packets;
            _results = results;
            _packetCount = packetCount;
            _cancellationToken = cancellationToken;
            Volatile.Write(ref _failure, null);
            _barrier!.SignalAndWait();
            _barrier.SignalAndWait();
            Volatile.Read(ref _failure)?.Throw();
            return true;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;
        if (_workerCount == 1) return;
        lock (_requestGate) _barrier!.SignalAndWait();
        Task.WaitAll(_workers);
        _barrier!.Dispose();
    }

    private void RunWorker(int workerIndex)
    {
        while (true)
        {
            _barrier!.SignalAndWait();
            if (Volatile.Read(ref _isDisposed) != 0) return;
            try { DecryptPartition(workerIndex); }
            catch (Exception exception) { Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(exception), null); }
            _barrier.SignalAndWait();
        }
    }

    private void DecryptPartition(int workerIndex)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        int batchCount = (_packetCount + 127) / 128;
        int firstBatch = (workerIndex * batchCount) / _workerCount;
        int lastBatch = ((workerIndex + 1) * batchCount) / _workerCount;
        int firstPacket = firstBatch * 128;
        int lastPacket = Math.Min(_packetCount, lastBatch * 128);
        int partitionPacketCount = lastPacket - firstPacket;
        if (partitionPacketCount == 0) return;

        if (!_decryptor.TryDecryptPackets(
            _packets!.AsSpan(firstPacket * TransportPacket.Size, partitionPacketCount * TransportPacket.Size),
            _results!.AsSpan(firstPacket, partitionPacketCount)))
            throw new InvalidOperationException("Packet partition decryption failed.");
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) != 0) throw new ObjectDisposedException(nameof(PacketDecryptionWorkerGroup));
    }
}
