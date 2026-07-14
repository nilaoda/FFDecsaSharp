using FFDecsaSharp.Gui.Services;
using FFDecsaSharp.Gui.Models;
using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.Tests.Gui;

public sealed class CsaBenchmarkServiceTests
{
    [Fact]
    public void BenchmarkUsesOneFixedBatchSize()
    {
        Assert.Equal(128, CsaBenchmarkService.BatchSize);
    }

    [Fact]
    public void BenchmarkWorkloadUsesTheCurrentMeasurementDefaultAtTheFourthOfTenStops()
    {
        Assert.Equal(15_000, CsaBenchmarkService.DefaultMeasurementBatches);
        Assert.Equal(10, CsaBenchmarkService.MeasurementBatchOptionValues.Count);
        Assert.Equal(CsaBenchmarkService.DefaultMeasurementBatches, CsaBenchmarkService.MeasurementBatchOptionValues[3]);
    }

    [Fact]
    public void BenchmarkWorkloadIsCoercedToTheNearestSliderStop()
    {
        Assert.Equal(15_000, CsaBenchmarkService.CoerceMeasurementBatches(15_001));
        Assert.Equal(1_000_000, CsaBenchmarkService.CoerceMeasurementBatches(int.MaxValue));
    }

    [Fact]
    public void DecryptionWorkerCountIsClampedToTheAvailableProcessors()
    {
        Assert.Equal(1, AppSettingsService.CoerceDecryptionWorkerCount(0));
        Assert.Equal(AppSettingsService.MaximumDecryptionWorkerCount, AppSettingsService.CoerceDecryptionWorkerCount(int.MaxValue));
    }

    [Fact]
    public void InvalidThemeModeFallsBackToTheSystemTheme()
    {
        Assert.Equal(AppThemeMode.System, AppSettingsService.CoerceThemeMode((AppThemeMode)99));
    }

    [Fact]
    public void PacketBlockHasAtLeastOneBitsliceBatchPerConfiguredWorker()
    {
        int workerCount = Math.Min(2, AppSettingsService.MaximumDecryptionWorkerCount);
        Assert.True(TransportStreamDecryptionService.GetPacketsPerBlock(workerCount) >= workerCount * CsaBenchmarkService.BatchSize);
    }
}
