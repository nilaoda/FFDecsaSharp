using FFDecsaSharp.Gui.Services;

namespace FFDecsaSharp.Gui.Models;

internal sealed class AppSettings
{
    public LanguageMode LanguageMode { get; init; } = LanguageMode.Auto;

    public AppThemeMode ThemeMode { get; init; } = AppThemeMode.System;

    public int DecryptionWorkerCount { get; init; } = 1;

    public int BenchmarkMeasurementBatches { get; init; } = 15_000;
}
