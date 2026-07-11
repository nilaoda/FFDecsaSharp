using System.Diagnostics;
using FFDecsaSharp.CSA;

namespace FFDecsaSharp.PerfHarness;

internal static class Program
{
    private const int BatchSize = 128;
    private const int WarmupIterations = 5_000;
    private const int MeasurementIterations = 30_000;

    private static int Main()
    {
        ReadOnlySpan<byte> even = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];
        ReadOnlySpan<byte> odd = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        if (!ControlWords.TryCreate(even, odd, out ControlWords controlWords)
            || !Decryptor.TryCreate(controlWords, out Decryptor? decryptor))
        {
            return 1;
        }

        byte[] source = new byte[188 * BatchSize];
        byte[] packets = new byte[source.Length];
        PacketDecryptionResult[] results = new PacketDecryptionResult[BatchSize];
        for (int packetIndex = 0; packetIndex < BatchSize; packetIndex++)
        {
            Span<byte> packet = source.AsSpan(packetIndex * 188, 188);
            packet[0] = 0x47;
            packet[3] = 0xD0;
            for (int byteIndex = 4; byteIndex < packet.Length; byteIndex++)
            {
                packet[byteIndex] = (byte)((packetIndex * 29) + (byteIndex * 37));
            }
        }

        for (int iteration = 0; iteration < WarmupIterations; iteration++)
        {
            Run(decryptor!, source, packets, results);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int iteration = 0; iteration < MeasurementIterations; iteration++)
        {
            Run(decryptor!, source, packets, results);
        }

        stopwatch.Stop();
        double nanosecondsPerPacket = stopwatch.Elapsed.TotalNanoseconds / (MeasurementIterations * BatchSize);
        Console.WriteLine($"{nanosecondsPerPacket:F1} ns/packet");
        return 0;
    }

    private static void Run(Decryptor decryptor, byte[] source, byte[] packets, PacketDecryptionResult[] results)
    {
        source.CopyTo(packets, 0);
        if (!decryptor.TryDecryptPackets(packets, results))
        {
            throw new InvalidOperationException("Batch decryption failed.");
        }
    }
}
