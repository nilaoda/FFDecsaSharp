using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.Gui.Services;

internal readonly record struct CsaBenchmarkResult(double BytesPerSecond, double ElapsedSeconds, long PacketsProcessed);

/// <summary>GUI adapter for the shared deterministic DVB-CSA benchmark.</summary>
internal static class CsaBenchmarkService
{
    public const int BatchSize = DvbCsaBenchmarkService.BatchSize;
    public const int WarmupBatches = DvbCsaBenchmarkService.WarmupBatches;
    public const int DefaultMeasurementBatches = DvbCsaBenchmarkService.DefaultMeasurementBatches;

    public static IReadOnlyList<int> MeasurementBatchOptionValues => DvbCsaBenchmarkService.MeasurementBatchOptionValues;

    public static async Task<CsaBenchmarkResult> RunAsync(int workerCount, int measurementBatches, CancellationToken cancellationToken = default)
    {
        DvbCsaBenchmarkResult result = await DvbCsaBenchmarkService.RunAsync(workerCount, measurementBatches, cancellationToken).ConfigureAwait(false);
        return new CsaBenchmarkResult(result.BytesPerSecond, result.ElapsedSeconds, result.PacketsProcessed);
    }

    public static int CoerceMeasurementBatches(int measurementBatches) => DvbCsaBenchmarkService.CoerceMeasurementBatches(measurementBatches);
}
