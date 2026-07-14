using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFDecsaSharp.Gui.Helpers;
using FFDecsaSharp.Gui.Models;
using FFDecsaSharp.Gui.Services;

namespace FFDecsaSharp.Gui.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private CancellationTokenSource? _benchmarkCts;

    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private bool _isBenchmarkRunning;
    [ObservableProperty] private string _benchmarkResult = "-";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private int _decryptionWorkerCount;
    [ObservableProperty] private double _benchmarkWorkloadIndex = 3;
    [ObservableProperty] private ThemeOption? _selectedTheme;

    public ObservableCollection<LanguageOption> LanguageOptions { get; } = [];
    public ObservableCollection<ThemeOption> ThemeOptions { get; } = [];
    public bool CanRunBenchmark => !IsBenchmarkRunning;
    public int MaximumDecryptionWorkerCount => AppSettingsService.MaximumDecryptionWorkerCount;
    public string DecryptionWorkerCountHint => L.Settings_DecryptionThreadsHint(MaximumDecryptionWorkerCount);
    public double MaximumBenchmarkWorkloadIndex => CsaBenchmarkService.MeasurementBatchOptionValues.Count - 1;
    public string BenchmarkWorkloadText => L.Benchmark_WorkloadBatches(BenchmarkMeasurementBatches, BenchmarkMeasurementBatches * CsaBenchmarkService.BatchSize);
    public string BenchmarkWorkloadHint => L.Benchmark_WorkloadHint;
    public string BenchmarkWorkloadMinimumText => CsaBenchmarkService.MeasurementBatchOptionValues[0].ToString("N0");
    public string BenchmarkWorkloadMaximumText => CsaBenchmarkService.MeasurementBatchOptionValues[^1].ToString("N0");
    private int BenchmarkMeasurementBatches => CsaBenchmarkService.MeasurementBatchOptionValues[(int)BenchmarkWorkloadIndex];

    public SettingsViewModel()
    {
        DecryptionWorkerCount = AppSettingsService.DecryptionWorkerCount;
        BenchmarkWorkloadIndex = GetBenchmarkWorkloadIndex(AppSettingsService.BenchmarkMeasurementBatches);
        RefreshLanguageOptions(AppSettingsService.LanguageMode);
        RefreshThemeOptions(AppSettingsService.ThemeMode);
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
    }

    [RelayCommand]
    private async Task RunBenchmarkAsync()
    {
        if (IsBenchmarkRunning) return;
        IsBenchmarkRunning = true;
        BenchmarkResult = "-";
        StatusText = L.Benchmark_Running;
        _benchmarkCts = new CancellationTokenSource();
        try
        {
            CsaBenchmarkResult result = await CsaBenchmarkService.RunAsync(DecryptionWorkerCount, BenchmarkMeasurementBatches, _benchmarkCts.Token);
            BenchmarkResult = ThroughputFormatter.Format(result.BytesPerSecond);
            StatusText = "";
        }
        catch (OperationCanceledException)
        {
            BenchmarkResult = "-";
            StatusText = "";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBenchmarkRunning = false;
            _benchmarkCts?.Dispose();
            _benchmarkCts = null;
        }
    }

    public bool TrySave(out Exception? exception) => AppSettingsService.TrySave(
        SelectedLanguage?.Mode ?? AppSettingsService.LanguageMode,
        DecryptionWorkerCount,
        BenchmarkMeasurementBatches,
        SelectedTheme?.Mode ?? AppSettingsService.ThemeMode,
        out exception);

    partial void OnDecryptionWorkerCountChanged(int value)
    {
        int coerced = AppSettingsService.CoerceDecryptionWorkerCount(value);
        if (value != coerced)
        {
            DecryptionWorkerCount = coerced;
        }

    }

    partial void OnIsBenchmarkRunningChanged(bool value) => OnPropertyChanged(nameof(CanRunBenchmark));

    partial void OnBenchmarkWorkloadIndexChanged(double value)
    {
        double coerced = Math.Clamp(Math.Round(value), 0, MaximumBenchmarkWorkloadIndex);
        if (value != coerced)
        {
            BenchmarkWorkloadIndex = coerced;
            return;
        }

        OnPropertyChanged(nameof(BenchmarkWorkloadText));
    }

    private static int GetBenchmarkWorkloadIndex(int measurementBatches)
    {
        IReadOnlyList<int> options = CsaBenchmarkService.MeasurementBatchOptionValues;
        int coerced = CsaBenchmarkService.CoerceMeasurementBatches(measurementBatches);
        for (int index = 0; index < options.Count; index++)
        {
            if (options[index] == coerced) return index;
        }

        return 0;
    }

    private void RefreshLanguageOptions(LanguageMode selectedMode)
    {
        LanguageOptions.Clear();
        LanguageOptions.Add(new LanguageOption(LanguageMode.Auto, L.App_Auto));
        LanguageOptions.Add(new LanguageOption(LanguageMode.English, L.App_English));
        LanguageOptions.Add(new LanguageOption(LanguageMode.SimplifiedChinese, L.App_Simplified));
        LanguageOptions.Add(new LanguageOption(LanguageMode.TraditionalChinese, L.App_Traditional));
        SelectedLanguage = LanguageOptions.FirstOrDefault(option => option.Mode == selectedMode) ?? LanguageOptions[0];
    }

    private void RefreshThemeOptions(AppThemeMode selectedMode)
    {
        ThemeOptions.Clear();
        ThemeOptions.Add(new ThemeOption(AppThemeMode.System, L.Settings_ThemeSystem));
        ThemeOptions.Add(new ThemeOption(AppThemeMode.Light, L.Settings_ThemeLight));
        ThemeOptions.Add(new ThemeOption(AppThemeMode.Dark, L.Settings_ThemeDark));
        SelectedTheme = ThemeOptions.FirstOrDefault(option => option.Mode == selectedMode) ?? ThemeOptions[0];
    }

    public void ApplySavedAppearance()
    {
        LocalizationService.Apply(SelectedLanguage?.Mode ?? AppSettingsService.LanguageMode);
        AppThemeService.Apply(SelectedTheme?.Mode ?? AppSettingsService.ThemeMode);
    }
}
