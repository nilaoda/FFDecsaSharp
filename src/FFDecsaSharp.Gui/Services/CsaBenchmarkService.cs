using System.Diagnostics;
using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Gui.Services;

internal readonly record struct CsaBenchmarkResult(double BytesPerSecond, double ElapsedSeconds, long PacketsProcessed);

internal static class CsaBenchmarkService
{
    public const int BatchSize = 128;
    public const int WarmupBatches = 2_000;
    public const int MeasurementBatches = 15_000;
    private const int PacketSize = 188;
    private const int PayloadSize = 184;
    public static Task<CsaBenchmarkResult> RunAsync(CancellationToken cancellationToken = default) => Task.Run(() => Run(BatchSize, cancellationToken), cancellationToken);

    private static CsaBenchmarkResult Run(int batchSize, CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> even = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        ReadOnlySpan<byte> odd = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];
        if (!ControlWords.TryCreate(even, odd, out var words) || !Decryptor.TryCreate(words, out var decryptor))
            throw new InvalidOperationException("Unable to initialize the benchmark decryptor.");

        var source = new byte[PacketSize * batchSize];
        var packets = new byte[source.Length];
        var results = new PacketDecryptionResult[batchSize];
        for (int packetIndex = 0; packetIndex < batchSize; packetIndex++)
        {
            Span<byte> packet = source.AsSpan(packetIndex * PacketSize, PacketSize);
            packet[0] = 0x47;
            packet[3] = 0xD0;
            for (int payloadIndex = 0; payloadIndex < PayloadSize; payloadIndex++) packet[payloadIndex + 4] = (byte)((packetIndex * 29) + (payloadIndex * 37));
        }

        for (int iteration = 0; iteration < WarmupBatches; iteration++)
        {
            source.CopyTo(packets, 0);
            if (!decryptor!.TryDecryptPackets(packets, results)) throw new InvalidOperationException("Benchmark warmup failed.");
        }

        long elapsedTicks = 0;
        for (int iteration = 0; iteration < MeasurementBatches; iteration++)
        {
            if ((iteration & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            source.CopyTo(packets, 0);
            long started = Stopwatch.GetTimestamp();
            if (!decryptor!.TryDecryptPackets(packets, results)) throw new InvalidOperationException("Benchmark measurement failed.");
            elapsedTicks += Stopwatch.GetTimestamp() - started;
        }

        long packetCount = (long)MeasurementBatches * batchSize;
        double elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;
        double bytesPerSecond = packetCount * (double)PayloadSize / elapsedSeconds;
        return new CsaBenchmarkResult(bytesPerSecond, elapsedSeconds, packetCount);
    }
}
