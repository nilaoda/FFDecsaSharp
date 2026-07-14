using System.Text.Json;
using FFDecsaSharp.Gui.Models;

namespace FFDecsaSharp.Gui.Services;

internal static class AppSettingsService
{
    private static int _decryptionWorkerCount = 1;
    private static int _benchmarkMeasurementBatches = CsaBenchmarkService.DefaultMeasurementBatches;
    private static LanguageMode _languageMode = LanguageMode.Auto;
    private static AppThemeMode _themeMode = AppThemeMode.System;

    public static event EventHandler? DecryptionWorkerCountChanged;

    public static int MaximumDecryptionWorkerCount => Math.Max(1, Environment.ProcessorCount);

    public static int DecryptionWorkerCount => Volatile.Read(ref _decryptionWorkerCount);

    public static int BenchmarkMeasurementBatches => Volatile.Read(ref _benchmarkMeasurementBatches);

    public static AppThemeMode ThemeMode => _themeMode;

    public static LanguageMode LanguageMode => _languageMode;

    public static void Load()
    {
        int workerCount = 1;
        int measurementBatches = CsaBenchmarkService.DefaultMeasurementBatches;
        AppThemeMode themeMode = AppThemeMode.System;
        LanguageMode languageMode = LanguageMode.Auto;
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                using var stream = File.OpenRead(AppPaths.SettingsFile);
                AppSettings settings = JsonSerializer.Deserialize(stream, AppJsonContext.Default.AppSettings) ?? new AppSettings();
                workerCount = settings.DecryptionWorkerCount;
                languageMode = settings.LanguageMode;
                themeMode = settings.ThemeMode;
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
        _themeMode = CoerceThemeMode(themeMode);
        _languageMode = CoerceLanguageMode(languageMode);
    }

    public static bool TrySave(LanguageMode languageMode, int workerCount, int measurementBatches, AppThemeMode themeMode, out Exception? exception)
    {
        int coercedWorkerCount = CoerceDecryptionWorkerCount(workerCount);
        int coercedMeasurementBatches = CsaBenchmarkService.CoerceMeasurementBatches(measurementBatches);
        AppThemeMode coercedThemeMode = CoerceThemeMode(themeMode);
        LanguageMode coercedLanguageMode = CoerceLanguageMode(languageMode);
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            using var stream = File.Create(AppPaths.SettingsFile);
            JsonSerializer.Serialize(stream, new AppSettings
            {
                LanguageMode = coercedLanguageMode,
                ThemeMode = coercedThemeMode,
                DecryptionWorkerCount = coercedWorkerCount,
                BenchmarkMeasurementBatches = coercedMeasurementBatches,
            }, AppJsonContext.Default.AppSettings);
            SetDecryptionWorkerCount(coercedWorkerCount, true);
            Volatile.Write(ref _benchmarkMeasurementBatches, coercedMeasurementBatches);
            _themeMode = coercedThemeMode;
            _languageMode = coercedLanguageMode;
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

    public static AppThemeMode CoerceThemeMode(AppThemeMode themeMode) => Enum.IsDefined(themeMode) ? themeMode : AppThemeMode.System;

    public static LanguageMode CoerceLanguageMode(LanguageMode languageMode) => Enum.IsDefined(languageMode) ? languageMode : LanguageMode.Auto;

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
