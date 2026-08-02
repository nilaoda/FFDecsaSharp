using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFDecsaSharp.Gui.Helpers;
using FFDecsaSharp.Gui.Models;
using FFDecsaSharp.Gui.Services;

namespace FFDecsaSharp.Gui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _runningTasks = new();
    private CancellationTokenSource _globalCts = new();
    private bool _queueLoopRunning;

    [ObservableProperty] private DecryptionTask? _selectedTask;
    [ObservableProperty] private string _statusText = L.App_Footer;
    [ObservableProperty] private string _globalSpeedText = ThroughputFormatter.Zero;
    [ObservableProperty] private int _maxConcurrency = 1;
    [ObservableProperty] private bool _isDetailPaneOpen;

    public ObservableCollection<DecryptionTask> Tasks { get; } = [];
    public int[] ConcurrencyOptions { get; } = [1, 2, 3, 5];
    public string AppTitle => $"{L.App_Title} v{AppVersion.Current}";
    public string DetailPaneButtonText => IsDetailPaneOpen ? L.Main_CollapseDetails : L.Main_ShowDetails;
    public string DecryptionWorkerCountText => AppSettingsService.DecryptionWorkerCount.ToString();

    public bool HasTasks => Tasks.Count > 0;
    public bool HasNoTasks => !HasTasks;
    public string QueueSummary => L.App_TaskSummary(Tasks.Count);
    public bool HasRunningTasks => _runningTasks.Count > 0;
    public bool HasQueuedTasks => Tasks.Any(t => t.StatusKey == LocKeys.Status_Queued);
    public bool CanStartSelected => !HasRunningTasks || _runningTasks.Count < EffectiveMaxConcurrency;
    public bool CanDeleteSelected => SelectedTask is { IsRunning: false };
    public bool CanStopQueue => HasRunningTasks || HasQueuedTasks;
    public bool CanStartAll => HasQueuedTasks && (!HasRunningTasks || _runningTasks.Count < EffectiveMaxConcurrency);

    public MainWindowViewModel()
    {
        Tasks.CollectionChanged += Tasks_CollectionChanged;
        LocalizationService.LanguageChanged += LocalizationService_LanguageChanged;
        AppSettingsService.DecryptionWorkerCountChanged += AppSettingsService_DecryptionWorkerCountChanged;
    }

    [RelayCommand]
    private void StartSelected()
    {
        if (SelectedTask is null || SelectedTask.StatusKey != LocKeys.Status_Queued) return;
        EnqueueAndRun();
    }

    [RelayCommand]
    private void StartAll() => EnqueueAndRun();

    [RelayCommand]
    private void CancelQueue()
    {
        _globalCts.Cancel();
        _globalCts.Dispose();
        _globalCts = new CancellationTokenSource();
        RefreshQueueState();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedTask is not { IsRunning: false } task) return;
        Tasks.Remove(task);
        SelectedTask = Tasks.FirstOrDefault();
        RefreshQueueState();
    }

    [RelayCommand]
    private void ToggleDetailPane()
    {
        IsDetailPaneOpen = !IsDetailPaneOpen;
        OnPropertyChanged(nameof(DetailPaneButtonText));
    }

    private void EnqueueAndRun()
    {
        RefreshQueueState();
        if (!_queueLoopRunning) _ = QueueLoopAsync();
    }

    private async Task QueueLoopAsync()
    {
        _queueLoopRunning = true;
        try
        {
            while (!_globalCts.IsCancellationRequested)
            {
                DecryptionTask? taskToStart = null;
                if (_runningTasks.Count < EffectiveMaxConcurrency)
                    taskToStart = Tasks.FirstOrDefault(t => t.StatusKey == LocKeys.Status_Queued);

                if (taskToStart is not null)
                {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
                    if (_runningTasks.TryAdd(taskToStart.Id, cts))
                        _ = RunTaskAsync(taskToStart, cts);
                    await Task.Delay(50, _globalCts.Token).ContinueWith(static _ => { }, CancellationToken.None);
                }
                else if (HasRunningTasks)
                {
                    await Task.Delay(200, _globalCts.Token).ContinueWith(static _ => { }, CancellationToken.None);
                }
                else
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _queueLoopRunning = false;
            RefreshQueueState();
        }
    }

    private async Task RunTaskAsync(DecryptionTask task, CancellationTokenSource cts)
    {
        if (!ControlWordParser.TryParse(task.EvenKey, out byte[] evenKey) || !ControlWordParser.TryParse(task.OddKey, out byte[] oddKey))
        {
            task.SetStatus(LocKeys.Status_Failed);
            task.Log = L.Error_InvalidKeys;
            _runningTasks.TryRemove(task.Id, out _);
            return;
        }

        task.IsRunning = true;
        task.SetStatus(LocKeys.Status_Running);
        task.Log = L.Log_Started;
        long previousBytes = 0;
        TimeSpan previousElapsed = TimeSpan.Zero;
        var progress = new Progress<DecryptionProgress>(value =>
        {
            task.Progress = value.TotalBytes == 0 ? 0 : value.ProcessedBytes * 100d / value.TotalBytes;
            task.PacketCount = value.PacketCount.ToString("N0");
            task.DecryptedCount = value.DecryptedCount.ToString("N0");
            task.Elapsed = FormatDuration(value.Elapsed);
            double seconds = (value.Elapsed - previousElapsed).TotalSeconds;
            if (seconds > 0)
            {
                double bps = (value.ProcessedBytes - previousBytes) / seconds;
                task.Speed = ThroughputFormatter.Format(bps);
                task.RawSpeed = bps;
                previousBytes = value.ProcessedBytes;
                previousElapsed = value.Elapsed;
            }
            task.Eta = EstimateRemaining(value);
            UpdateGlobalSpeed();
        });

        try
        {
            DecryptionSummary summary = await TsDecryptionService.DecryptAsync(task.InputPaths, task.OutputPath, evenKey, oddKey, task.PacketOffset, task.PacketLimit, progress, cts.Token);
            task.Progress = 100;
            task.PacketCount = summary.PacketCount.ToString("N0");
            task.DecryptedCount = summary.DecryptedCount.ToString("N0");
            task.Elapsed = FormatDuration(summary.Elapsed);
            task.Eta = "-";
            task.SetStatus(LocKeys.Status_Completed);
            task.Log += Environment.NewLine + L.Log_Completed;
        }
        catch (OperationCanceledException)
        {
            task.SetStatus(LocKeys.Status_Canceled);
        }
        catch (Exception ex)
        {
            task.SetStatus(LocKeys.Status_Failed);
            task.Log += Environment.NewLine + ex;
        }
        finally
        {
            task.IsRunning = false;
            if (task.StatusKey is not LocKeys.Status_Completed)
            {
                task.Speed = ThroughputFormatter.Zero;
                task.RawSpeed = 0;
                task.Eta = "-";
            }
            cts.Dispose();
            _runningTasks.TryRemove(task.Id, out _);
            UpdateGlobalSpeed();
            RefreshQueueState();
        }
    }

    private void UpdateGlobalSpeed()
    {
        if (_runningTasks.Count == 0) return;
        double total = 0;
        foreach (DecryptionTask task in Tasks.Where(t => t.IsRunning))
            total += task.RawSpeed;
        GlobalSpeedText = total > 0 ? ThroughputFormatter.Format(total) : ThroughputFormatter.Zero;
    }

    private void RefreshQueueState()
    {
        OnPropertyChanged(nameof(HasRunningTasks));
        OnPropertyChanged(nameof(HasQueuedTasks));
        OnPropertyChanged(nameof(CanStartSelected));
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(CanStopQueue));
        OnPropertyChanged(nameof(CanStartAll));
        OnPropertyChanged(nameof(QueueSummary));
        int running = _runningTasks.Count;
        int queued = Tasks.Count(t => t.StatusKey == LocKeys.Status_Queued);
        if (running > 0)
            StatusText = L.Main_RunningCount(running, queued);
        else if (queued > 0)
            StatusText = L.Main_QueuedCount(queued);
        else
            StatusText = L.App_Footer;
    }

    partial void OnSelectedTaskChanged(DecryptionTask? value)
    {
        OnPropertyChanged(nameof(CanStartSelected));
        OnPropertyChanged(nameof(CanDeleteSelected));
    }

    partial void OnMaxConcurrencyChanged(int value)
    {
        int coerced = CoerceConcurrency(value);
        if (value != coerced) MaxConcurrency = coerced;
        RefreshQueueState();
    }

    private int CoerceConcurrency(int value) => ConcurrencyOptions.Contains(value) ? value : 1;

    private int EffectiveMaxConcurrency => Math.Min(
        MaxConcurrency,
        Math.Max(1, Environment.ProcessorCount / AppSettingsService.DecryptionWorkerCount));

    private void AppSettingsService_DecryptionWorkerCountChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(DecryptionWorkerCountText));
        RefreshQueueState();
    }

    private void Tasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasNoTasks));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(HasQueuedTasks));
        RefreshQueueState();
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e)
    {
        foreach (DecryptionTask task in Tasks) task.RefreshLocalizedText();
        OnPropertyChanged(nameof(AppTitle));
        OnPropertyChanged(nameof(DetailPaneButtonText));
        RefreshQueueState();
    }

    private static string EstimateRemaining(DecryptionProgress progress)
    {
        if (progress.ProcessedBytes <= 0 || progress.Elapsed <= TimeSpan.Zero || progress.TotalBytes <= progress.ProcessedBytes) return "-";
        double averageBytesPerSecond = progress.ProcessedBytes / progress.Elapsed.TotalSeconds;
        if (averageBytesPerSecond <= 0 || double.IsNaN(averageBytesPerSecond) || double.IsInfinity(averageBytesPerSecond)) return "-";
        return FormatDuration(TimeSpan.FromSeconds((progress.TotalBytes - progress.ProcessedBytes) / averageBytesPerSecond));
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value:mm\\:ss}"
            : value.ToString("mm\\:ss");
    }
}
