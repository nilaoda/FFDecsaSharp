using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFDecsaSharp.Gui.Helpers;
using FFDecsaSharp.Gui.Models;
using FFDecsaSharp.Gui.Services;

namespace FFDecsaSharp.Gui.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private bool _suppressLanguageSelection;
    private CancellationTokenSource? _benchmarkCts;

    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private bool _isBenchmarkRunning;
    [ObservableProperty] private string _benchmarkResult = "-";
    [ObservableProperty] private string _statusText = "";

    public ObservableCollection<LanguageOption> LanguageOptions { get; } = [];
    public bool CanRunBenchmark => !IsBenchmarkRunning;

    public SettingsViewModel()
    {
        RefreshLanguageOptions(LocalizationService.Mode);
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (!_suppressLanguageSelection && value is not null) LocalizationService.Apply(value.Mode);
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
            CsaBenchmarkResult result = await CsaBenchmarkService.RunAsync(_benchmarkCts.Token);
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

    private void RefreshLanguageOptions(LanguageMode selectedMode)
    {
        _suppressLanguageSelection = true;
        try
        {
            LanguageOptions.Clear();
            LanguageOptions.Add(new LanguageOption(LanguageMode.Auto, L.App_Auto));
            LanguageOptions.Add(new LanguageOption(LanguageMode.English, L.App_English));
            LanguageOptions.Add(new LanguageOption(LanguageMode.SimplifiedChinese, L.App_Simplified));
            LanguageOptions.Add(new LanguageOption(LanguageMode.TraditionalChinese, L.App_Traditional));
            SelectedLanguage = LanguageOptions.FirstOrDefault(option => option.Mode == selectedMode) ?? LanguageOptions[0];
        }
        finally { _suppressLanguageSelection = false; }
    }

    public LanguageMode GetOriginalLanguage() => LocalizationService.Mode;
}
