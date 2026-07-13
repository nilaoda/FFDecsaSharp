using System.Text.Json;
using FFDecsaSharp.Gui.Models;

namespace FFDecsaSharp.Gui.Services;

internal static class AppSettingsService
{
    private static int _decryptionWorkerCount = 1;
    private static int _benchmarkMeasurementBatches = CsaBenchmarkService.DefaultMeasurementBatches;

    public static event EventHandler? DecryptionWorkerCountChanged;

    public static int MaximumDecryptionWorkerCount => Math.Max(1, Environment.ProcessorCount);

    public static int DecryptionWorkerCount => Volatile.Read(ref _decryptionWorkerCount);

    public static int BenchmarkMeasurementBatches => Volatile.Read(ref _benchmarkMeasurementBatches);

    public static void Load()
    {
        int workerCount = 1;
        int measurementBatches = CsaBenchmarkService.DefaultMeasurementBatches;
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                using var stream = File.OpenRead(AppPaths.SettingsFile);
                AppSettings settings = JsonSerializer.Deserialize(stream, AppJsonContext.Default.AppSettings) ?? new AppSettings();
                workerCount = settings.DecryptionWorkerCount;
                measurementBatches = settings.BenchmarkMeasurementBatches == 0
                    ? CsaBenchmarkService.DefaultMeasurementBatches
                    : settings.BenchmarkMeasurementBatches;
            }
        }
        catch
        {
            workerCount = 1;
        }

        SetDecryptionWorkerCount(workerCount, false);
        Volatile.Write(ref _benchmarkMeasurementBatches, CsaBenchmarkService.CoerceMeasurementBatches(measurementBatches));
    }

    public static bool TrySave(int workerCount, int measurementBatches, out Exception? exception)
    {
        int coercedWorkerCount = CoerceDecryptionWorkerCount(workerCount);
        int coercedMeasurementBatches = CsaBenchmarkService.CoerceMeasurementBatches(measurementBatches);
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            using var stream = File.Create(AppPaths.SettingsFile);
            JsonSerializer.Serialize(stream, new AppSettings
            {
                DecryptionWorkerCount = coercedWorkerCount,
                BenchmarkMeasurementBatches = coercedMeasurementBatches,
            }, AppJsonContext.Default.AppSettings);
            SetDecryptionWorkerCount(coercedWorkerCount, true);
            Volatile.Write(ref _benchmarkMeasurementBatches, coercedMeasurementBatches);
            exception = null;
            return true;
        }
        catch (Exception error)
        {
            exception = error;
            return false;
        }
    }

    public static int CoerceDecryptionWorkerCount(int workerCount) => Math.Clamp(workerCount, 1, MaximumDecryptionWorkerCount);

    private static void SetDecryptionWorkerCount(int workerCount, bool notify)
    {
        int coercedWorkerCount = CoerceDecryptionWorkerCount(workerCount);
        int previousWorkerCount = Interlocked.Exchange(ref _decryptionWorkerCount, coercedWorkerCount);
        if (notify && previousWorkerCount != coercedWorkerCount)
        {
            DecryptionWorkerCountChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
