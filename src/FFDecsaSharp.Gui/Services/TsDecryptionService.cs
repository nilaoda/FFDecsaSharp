using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.Gui.Services;

internal readonly record struct DecryptionProgress(long ProcessedBytes, long TotalBytes, long PacketCount, long DecryptedCount, TimeSpan Elapsed);
internal readonly record struct DecryptionSummary(long PacketCount, long DecryptedCount, TimeSpan Elapsed);

/// <summary>GUI adapter for the shared, settings-independent transport-stream service.</summary>
internal static class TsDecryptionService
{
    public const int DefaultPacketsPerBlock = TransportStreamDecryptionService.DefaultPacketsPerBlock;

    public static async Task<DecryptionSummary> DecryptAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        byte[] evenKey,
        byte[] oddKey,
        long packetOffset,
        long packetLimit,
        IProgress<DecryptionProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        IProgress<TransportStreamDecryptionProgress>? sharedProgress = progress is null
            ? null
            : new Progress<TransportStreamDecryptionProgress>(value => progress.Report(
                new DecryptionProgress(value.ProcessedBytes, value.TotalBytes, value.PacketCount, value.DecryptedCount, value.Elapsed)));

        TransportStreamDecryptionSummary summary = await TransportStreamDecryptionService.DecryptAsync(
            inputPaths,
            outputPath,
            evenKey,
            oddKey,
            packetOffset,
            packetLimit,
            AppSettingsService.DecryptionWorkerCount,
            sharedProgress,
            cancellationToken).ConfigureAwait(false);
        return new DecryptionSummary(summary.PacketCount, summary.DecryptedCount, summary.Elapsed);
    }
}
