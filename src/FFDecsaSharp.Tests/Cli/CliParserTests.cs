using FFDecsaSharp.Cli;
using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.Tests.Cli;

public sealed class CliParserTests
{
    [Fact]
    public void SingleControlWordIsUsedForBothKeyParities()
    {
        var command = Assert.IsType<DecryptCommand>(CliParser.Parse([
            "decrypt", "--input", "input.ts", "--output", "output.ts", "--cw", "010203040506"]).Command);

        Assert.Equal(command.EvenControlWord, command.OddControlWord);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x06, 0x04, 0x05, 0x06, 0x0F }, command.EvenControlWord);
        Assert.Equal(1, command.WorkerCount);
    }

    [Fact]
    public void SeparateControlWordsMustBeSpecifiedTogether()
    {
        Assert.Throws<CliUsageException>(() => CliParser.Parse([
            "decrypt", "--input", "input.ts", "--output", "output.ts", "--even-cw", "010203040506"]));
    }

    [Fact]
    public void BenchmarkDefaultsToOneWorkerAndTheStandardWorkload()
    {
        var command = Assert.IsType<BenchmarkCommand>(CliParser.Parse(["benchmark"]).Command);

        Assert.Equal(1, command.WorkerCount);
        Assert.Equal(15_000, command.MeasurementBatches);
    }

    [Fact]
    public async Task ManualLanguageSelectionLocalizesHelpAndBenchmarkOutput()
    {
        var output = new StringWriter();
        int helpExitCode = await CliApplication.RunAsync(["--lang", "zh-Hans", "--help"], output, new StringWriter(), CancellationToken.None);

        Assert.Equal(0, helpExitCode);
        Assert.Contains("命令行工具", output.ToString());

        output.GetStringBuilder().Clear();
        int benchmarkExitCode = await CliApplication.RunAsync(["benchmark", "--batches", "1000", "--lang", "zh-Hant"], output, new StringWriter(), CancellationToken.None);

        Assert.Equal(0, benchmarkExitCode);
        Assert.Contains("有效負載", output.ToString());
    }

    [Fact]
    public async Task DecryptCommandWritesAnAtomicOutputWithoutAnyGuiSettings()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FFDecsaSharp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string inputPath = Path.Combine(directory, "input.ts");
        string outputPath = Path.Combine(directory, "output.ts");
        try
        {
            var input = new byte[(188 * 3) + 3];
            input[0] = 0x01;
            input[1] = 0x02;
            for (int packet = 0; packet < 3; packet++)
            {
                int offset = 2 + (packet * 188);
                input[offset] = 0x47;
                input[offset + 3] = 0x10;
            }
            input[^1] = 0xAA;
            await File.WriteAllBytesAsync(inputPath, input);
            string directOutputPath = Path.Combine(directory, "direct-output.ts");
            TransportStreamDecryptionSummary directSummary = await TransportStreamDecryptionService.DecryptAsync(
                [inputPath], directOutputPath, new byte[8], new byte[8], 0, 0, 1);
            Assert.Equal(2, directSummary.SkippedLeadingBytes);
            Assert.Equal(1, directSummary.IgnoredTrailingBytes);

            var output = new StringWriter();
            var error = new StringWriter();
            int exitCode = await CliApplication.RunAsync(
                ["decrypt", "--input", inputPath, "--output", outputPath, "--cw", "000000000000", "--no-progress", "--lang", "en"],
                output,
                error,
                CancellationToken.None);

            Assert.True(exitCode == 0, error.ToString());
            Assert.Equal(input[2..^1], await File.ReadAllBytesAsync(outputPath));
            Assert.DoesNotContain(Directory.EnumerateFiles(directory), path => path.EndsWith(".partial", StringComparison.Ordinal));
            Assert.Contains("Average throughput:", output.ToString());
            Assert.Contains("Skipped 2 leading byte(s)", output.ToString());
            Assert.Contains("Ignored 1 trailing byte(s)", output.ToString());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
