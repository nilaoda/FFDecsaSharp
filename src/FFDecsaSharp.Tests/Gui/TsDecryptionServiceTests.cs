using FFDecsaSharp.CSA;
using FFDecsaSharp.Gui.Services;

namespace FFDecsaSharp.Tests.Gui;

public sealed class TsDecryptionServiceTests
{
    [Fact]
    public async Task DecryptAsyncResynchronizesAfterLeadingBytesAndIgnoresTrailingBytes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FFDecsaSharp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string inputPath = Path.Combine(directory, "input.ts");
        string outputPath = Path.Combine(directory, "output.ts");

        try
        {
            var input = new byte[5 + (188 * 3) + 2];
            input[0] = 0x01;
            input[1] = 0x02;
            input[2] = 0x03;
            input[3] = 0x04;
            input[4] = 0x05;
            for (var packet = 0; packet < 3; packet++)
            {
                int offset = 5 + (packet * 188);
                input[offset] = 0x47;
                input[offset + 3] = 0x10;
                input[offset + 4] = (byte)packet;
            }
            input[^2] = 0xAA;
            input[^1] = 0xBB;
            await File.WriteAllBytesAsync(inputPath, input);

            var summary = await TsDecryptionService.DecryptAsync([inputPath], outputPath, new byte[8], new byte[8], 0, 0, null!);

            Assert.Equal(3, summary.PacketCount);
            Assert.Equal(0, summary.DecryptedCount);
            byte[] output = await File.ReadAllBytesAsync(outputPath);
            Assert.Equal(188 * 3, output.Length);
            Assert.Equal(0x47, output[0]);
            Assert.Equal(0x47, output[188]);
            Assert.Equal(0x47, output[376]);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task DecryptAsyncMergesMultipleInputsIntoOneOutput()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FFDecsaSharp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string firstInput = Path.Combine(directory, "first.ts");
        string secondInput = Path.Combine(directory, "second.ts");
        string outputPath = Path.Combine(directory, "merged.ts");

        try
        {
            await File.WriteAllBytesAsync(firstInput, CreateTransportStream(2, 0x11));
            await File.WriteAllBytesAsync(secondInput, CreateTransportStream(3, 0x22));

            DecryptionSummary summary = await TsDecryptionService.DecryptAsync([firstInput, secondInput], outputPath, new byte[8], new byte[8], 0, 0, null!);

            Assert.Equal(5, summary.PacketCount);
            byte[] output = await File.ReadAllBytesAsync(outputPath);
            Assert.Equal(5 * 188, output.Length);
            Assert.Equal(0x11, output[4]);
            Assert.Equal(0x22, output[2 * 188 + 4]);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void PacketBlockDecryptionMatchesSingleWorkerOutput()
    {
        const int packetCount = 256;
        ReadOnlySpan<byte> even = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        ReadOnlySpan<byte> odd = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];
        Assert.True(ControlWords.TryCreate(even, odd, out ControlWords controlWords));
        Assert.True(Decryptor.TryCreate(controlWords, out Decryptor? decryptor));

        using var singleWorkerGroup = new PacketDecryptionWorkerGroup(decryptor!, 1);
        using var parallelWorkerGroup = new PacketDecryptionWorkerGroup(decryptor!, 2);
        for (int block = 0; block < 2; block++)
        {
            byte[] source = CreateScrambledTransportStream(packetCount, block);
            byte[] singleWorkerPackets = source.ToArray();
            byte[] parallelPackets = source.ToArray();
            var singleWorkerResults = new PacketDecryptionResult[packetCount];
            var parallelResults = new PacketDecryptionResult[packetCount];

            Assert.True(singleWorkerGroup.TryDecrypt(singleWorkerPackets, singleWorkerResults, packetCount, CancellationToken.None));
            Assert.True(parallelWorkerGroup.TryDecrypt(parallelPackets, parallelResults, packetCount, CancellationToken.None));

            Assert.Equal(singleWorkerPackets, parallelPackets);
            Assert.Equal(singleWorkerResults, parallelResults);
            Assert.All(parallelResults, result => Assert.Equal(PacketDecryptionResult.Decrypted, result));
        }
    }

    private static byte[] CreateTransportStream(int packetCount, byte marker)
    {
        var bytes = new byte[packetCount * 188];
        for (int packet = 0; packet < packetCount; packet++)
        {
            int offset = packet * 188;
            bytes[offset] = 0x47;
            bytes[offset + 3] = 0x10;
            bytes[offset + 4] = marker;
        }
        return bytes;
    }

    private static byte[] CreateScrambledTransportStream(int packetCount, int block = 0)
    {
        var bytes = new byte[packetCount * 188];
        for (int packet = 0; packet < packetCount; packet++)
        {
            int offset = packet * 188;
            bytes[offset] = 0x47;
            bytes[offset + 3] = 0xD0;
            for (int payloadIndex = 0; payloadIndex < 184; payloadIndex++)
            {
                bytes[offset + 4 + payloadIndex] = (byte)((block * 17) + (packet * 29) + (payloadIndex * 37));
            }
        }

        return bytes;
    }
}
