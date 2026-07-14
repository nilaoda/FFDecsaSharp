using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.Cli;

internal static class CliApplication
{
    private const int UsageError = 2;
    private const int ProcessingError = 3;
    private const int Cancelled = 130;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        CliLocalizer localizer = CliLocalizer.Create(CliLanguage.Auto);
        try
        {
            CliInvocation invocation = CliParser.Parse(args);
            localizer = invocation.Localizer;
            switch (invocation.Command)
            {
                case HelpCommand:
                    await output.WriteLineAsync(localizer.Usage).ConfigureAwait(false);
                    await output.WriteLineAsync(localizer.ProjectHomepage).ConfigureAwait(false);
                    return 0;
                case VersionCommand:
                    await output.WriteLineAsync(GetVersion()).ConfigureAwait(false);
                    return 0;
                case DecryptCommand decrypt:
                    return await RunDecryptAsync(decrypt, localizer, output, error, cancellationToken).ConfigureAwait(false);
                case BenchmarkCommand benchmark:
                    return await RunBenchmarkAsync(benchmark, localizer, output, cancellationToken).ConfigureAwait(false);
                default:
                    throw new InvalidOperationException("Unsupported CLI command.");
            }
        }
        catch (CliUsageException exception)
        {
            await error.WriteLineAsync($"{localizer.ErrorPrefix}: {exception.Message}").ConfigureAwait(false);
            await error.WriteLineAsync(localizer.UsageHint).ConfigureAwait(false);
            return UsageError;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync(localizer.Cancelled).ConfigureAwait(false);
            return Cancelled;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync($"{localizer.ErrorPrefix}: {exception.Message}").ConfigureAwait(false);
            return ProcessingError;
        }
    }

    private static async Task<int> RunDecryptAsync(DecryptCommand command, CliLocalizer localizer, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        string outputPath = Path.GetFullPath(command.OutputPath);
        string[] inputPaths = command.InputPaths.Select(Path.GetFullPath).ToArray();
        if (inputPaths.Any(path => string.Equals(path, outputPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            throw new CliUsageException(localizer.OutputMatchesInput);
        if (File.Exists(outputPath) && !command.Overwrite)
            throw new CliUsageException(localizer.OutputExists(outputPath));

        string temporaryPath = outputPath + ".ffdecsasharp-" + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            long previousBytes = 0;
            TimeSpan previousElapsed = TimeSpan.Zero;
            if (command.ShowProgress)
                await error.WriteLineAsync(localizer.DecryptionStarting(inputPaths.Length, outputPath, command.WorkerCount)).ConfigureAwait(false);

            var progress = command.ShowProgress
                ? new Progress<TransportStreamDecryptionProgress>(value =>
                {
                    double percent = value.TotalBytes == 0 ? 0 : value.ProcessedBytes * 100d / value.TotalBytes;
                    double speed = value.Elapsed > previousElapsed
                        ? (value.ProcessedBytes - previousBytes) / (value.Elapsed - previousElapsed).TotalSeconds
                        : 0;
                    previousBytes = value.ProcessedBytes;
                    previousElapsed = value.Elapsed;
                    error.Write($"\r{percent,6:F2}%  {value.PacketCount:N0} packets  {FormatBytes(speed)}/s");
                })
                : null;

            TransportStreamDecryptionSummary summary = await TransportStreamDecryptionService.DecryptAsync(
                inputPaths,
                temporaryPath,
                command.EvenControlWord,
                command.OddControlWord,
                command.PacketOffset,
                command.PacketLimit,
                command.WorkerCount,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (command.ShowProgress) await error.WriteLineAsync().ConfigureAwait(false);
            File.Move(temporaryPath, outputPath, command.Overwrite);
            if (command.Json)
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(
                    new DecryptJsonResult(outputPath, summary.PacketCount, summary.DecryptedCount, summary.SkippedLeadingBytes, summary.IgnoredTrailingBytes, summary.Elapsed.TotalSeconds),
                    CliJsonContext.Default.DecryptJsonResult)).ConfigureAwait(false);
            }
            else
            {
                await output.WriteLineAsync(localizer.DecryptionComplete(summary.DecryptedCount, summary.PacketCount, summary.Elapsed.TotalSeconds)).ConfigureAwait(false);
                double averageBytesPerSecond = summary.PacketCount * 188d / summary.Elapsed.TotalSeconds;
                await output.WriteLineAsync(localizer.AverageThroughput(FormatBytes(averageBytesPerSecond))).ConfigureAwait(false);
                if (summary.SkippedLeadingBytes > 0)
                    await output.WriteLineAsync(localizer.SkippedLeadingBytes(summary.SkippedLeadingBytes)).ConfigureAwait(false);
                if (summary.IgnoredTrailingBytes > 0)
                    await output.WriteLineAsync(localizer.IgnoredTrailingBytes(summary.IgnoredTrailingBytes)).ConfigureAwait(false);
                await output.WriteLineAsync(localizer.OutputLabel(outputPath)).ConfigureAwait(false);
            }
            return 0;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static async Task<int> RunBenchmarkAsync(BenchmarkCommand command, CliLocalizer localizer, TextWriter output, CancellationToken cancellationToken)
    {
        DvbCsaBenchmarkResult result = await DvbCsaBenchmarkService.RunAsync(command.WorkerCount, command.MeasurementBatches, cancellationToken).ConfigureAwait(false);
        if (command.Json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                new BenchmarkJsonResult(command.WorkerCount, command.MeasurementBatches, result.BytesPerSecond, result.ElapsedSeconds, result.PacketsProcessed),
                CliJsonContext.Default.BenchmarkJsonResult)).ConfigureAwait(false);
        }
        else
        {
            await output.WriteLineAsync(localizer.BenchmarkResult(result.BytesPerSecond / 1_000_000d, command.WorkerCount, command.MeasurementBatches)).ConfigureAwait(false);
        }
        return 0;
    }

    private static string FormatBytes(double bytesPerSecond) => bytesPerSecond switch
    {
        >= 1_000_000_000d => $"{bytesPerSecond / 1_000_000_000d:F2} GB",
        >= 1_000_000d => $"{bytesPerSecond / 1_000_000d:F1} MB",
        >= 1_000d => $"{bytesPerSecond / 1_000d:F1} KB",
        _ => $"{bytesPerSecond.ToString("F0", CultureInfo.InvariantCulture)} B",
    };

    private static string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}

internal sealed record DecryptJsonResult(string Output, long Packets, long DecryptedPackets, long SkippedLeadingBytes, long IgnoredTrailingBytes, double ElapsedSeconds);
internal sealed record BenchmarkJsonResult(int Workers, int MeasurementBatches, double BytesPerSecond, double ElapsedSeconds, long Packets);

[JsonSerializable(typeof(DecryptJsonResult))]
[JsonSerializable(typeof(BenchmarkJsonResult))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
