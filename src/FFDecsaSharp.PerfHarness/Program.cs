using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using FFDecsaSharp.CSA;

namespace FFDecsaSharp.PerfHarness;

internal static class Program
{
    private const int BatchSize = 128;
    private const int PacketSize = 188;
    private const int PayloadSize = 184;
    private const int WarmupBatches = 5_000;
    private const int MeasurementBatches = 30_000;

    private static int Main()
    {
        ReadOnlySpan<byte> even = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        ReadOnlySpan<byte> odd = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];
        if (!ControlWords.TryCreate(even, odd, out ControlWords controlWords)
            || !Decryptor.TryCreate(controlWords, out Decryptor? decryptor))
        {
            return 1;
        }

        Decryptor initializedDecryptor = decryptor!;

        byte[] source = new byte[PacketSize * BatchSize];
        byte[] packets = new byte[source.Length];
        PacketDecryptionResult[] results = new PacketDecryptionResult[BatchSize];
        CreateSourcePackets(source);

        if (!DecryptOnce(initializedDecryptor, source, packets, results))
        {
            return 2;
        }

        ulong expectedHash = ComputeFnv1a64(packets);
        for (int iteration = 0; iteration < WarmupBatches; iteration++)
        {
            if (!DecryptOnce(initializedDecryptor, source, packets, results))
            {
                return 3;
            }
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long elapsedTicks = 0;
        for (int iteration = 0; iteration < MeasurementBatches; iteration++)
        {
            source.CopyTo(packets, 0);
            long start = Stopwatch.GetTimestamp();
            bool decrypted = initializedDecryptor.TryDecryptPackets(packets, results);
            elapsedTicks += Stopwatch.GetTimestamp() - start;
            if (!decrypted)
            {
                return 4;
            }
        }

        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        ulong actualHash = ComputeFnv1a64(packets);
        if (actualHash != expectedHash)
        {
            return 5;
        }

        double elapsedNanoseconds = elapsedTicks * (1_000_000_000d / Stopwatch.Frequency);
        double packetsProcessed = MeasurementBatches * (double)BatchSize;
        double nanosecondsPerPacket = elapsedNanoseconds / packetsProcessed;
        double packetsPerSecond = 1_000_000_000d / nanosecondsPerPacket;
        double megabitsPerSecond = (packetsPerSecond * PayloadSize * 8d) / 1_000_000d;
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"format\":\"ffdecsa-compare-v1\",\"implementation\":\"csharp\",\"runtime\":\"{Environment.Version}\",\"architecture\":\"{RuntimeInformation.ProcessArchitecture}\",\"parallelism\":{BatchSize},\"batch_packets\":{BatchSize},\"warmup_batches\":{WarmupBatches},\"measurement_batches\":{MeasurementBatches},\"timed_scope\":\"decrypt_only\",\"copy_in_timed_scope\":false,\"payload_bytes_per_packet\":{PayloadSize},\"packets_processed\":{packetsProcessed:F0},\"elapsed_ns\":{elapsedNanoseconds:F0},\"nanoseconds_per_packet\":{nanosecondsPerPacket:F3},\"packets_per_second\":{packetsPerSecond:F3},\"megabits_per_second\":{megabitsPerSecond:F3},\"managed_allocated_bytes\":{allocatedBytes},\"output_fnv1a64\":\"{actualHash:X16}\",\"verified\":true}}"));
        return 0;
    }

    private static void CreateSourcePackets(Span<byte> source)
    {
        for (int packetIndex = 0; packetIndex < BatchSize; packetIndex++)
        {
            Span<byte> packet = source.Slice(packetIndex * PacketSize, PacketSize);
            packet[0] = 0x47;
            packet[3] = 0xD0;
            for (int payloadIndex = 0; payloadIndex < PayloadSize; payloadIndex++)
            {
                packet[payloadIndex + 4] = (byte)((packetIndex * 29) + (payloadIndex * 37));
            }
        }
    }

    private static bool DecryptOnce(Decryptor decryptor, byte[] source, byte[] packets, PacketDecryptionResult[] results)
    {
        source.CopyTo(packets, 0);
        if (!decryptor.TryDecryptPackets(packets, results))
        {
            return false;
        }

        for (int index = 0; index < results.Length; index++)
        {
            if (results[index] != PacketDecryptionResult.Decrypted)
            {
                return false;
            }
        }

        return true;
    }

    private static ulong ComputeFnv1a64(ReadOnlySpan<byte> data)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte value in data)
        {
            hash = (hash ^ value) * 1099511628211UL;
        }

        return hash;
    }
}
