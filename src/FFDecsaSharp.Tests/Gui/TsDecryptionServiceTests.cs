using FFDecsaSharp.Gui;

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
}
