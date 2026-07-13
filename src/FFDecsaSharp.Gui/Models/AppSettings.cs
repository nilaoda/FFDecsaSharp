namespace FFDecsaSharp.Gui.Models;

internal sealed class AppSettings
{
    public int DecryptionWorkerCount { get; init; } = 1;

    public int BenchmarkMeasurementBatches { get; init; } = 15_000;
}
