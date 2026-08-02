using System.ComponentModel;
using FFDecsaSharp.Gui;
using FFDecsaSharp.Gui.Models;
using FFDecsaSharp.Gui.Services;
using FFDecsaSharp.Gui.ViewModels;

namespace FFDecsaSharp.Tests.Gui;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void StartSelectedStartsTheSelectedQueuedTask()
    {
        var viewModel = new MainWindowViewModel();
        DecryptionTask first = CreateTask("first.ts");
        DecryptionTask second = CreateTask("second.ts");
        viewModel.Tasks.Add(first);
        viewModel.Tasks.Add(second);
        viewModel.SelectedTask = second;

        viewModel.StartSelectedCommand.Execute(null);

        Assert.Equal(LocKeys.Status_Queued, first.StatusKey);
        Assert.Equal(LocKeys.Status_Failed, second.StatusKey);
    }

    [Fact]
    public void SelectedCommandsReflectTheSelectedTaskState()
    {
        var viewModel = new MainWindowViewModel();
        DecryptionTask task = CreateTask("selected.ts");
        viewModel.Tasks.Add(task);

        Assert.False(viewModel.CanStartSelected);
        Assert.False(viewModel.CanStopSelected);

        viewModel.SelectedTask = task;

        Assert.True(viewModel.CanStartSelected);
        Assert.True(viewModel.CanStopSelected);
    }

    [Fact]
    public void StopSelectedRemovesQueuedTaskAndStartSelectedRestartsIt()
    {
        var viewModel = new MainWindowViewModel();
        DecryptionTask task = CreateTask("restart.ts");
        viewModel.Tasks.Add(task);
        viewModel.SelectedTask = task;

        viewModel.StopSelectedCommand.Execute(null);

        Assert.Equal(LocKeys.Status_Stopped, task.StatusKey);
        Assert.False(viewModel.HasQueuedTasks);
        Assert.True(viewModel.CanStartSelected);

        task.Progress = 42;
        task.PacketCount = "100";
        task.DecryptedCount = "80";
        task.Elapsed = "00:10";
        task.RawSpeed = 1024;

        viewModel.StartSelectedCommand.Execute(null);

        Assert.Equal(LocKeys.Status_Failed, task.StatusKey);
        Assert.False(viewModel.CanStartSelected);
        Assert.Equal(0, task.Progress);
        Assert.Equal("-", task.PacketCount);
        Assert.Equal("-", task.DecryptedCount);
        Assert.Equal("-", task.Elapsed);
        Assert.Equal(0, task.RawSpeed);
    }

    [Fact]
    public void LanguageChangeRefreshesCachedMainWindowText()
    {
        LanguageMode originalLanguage = LocalizationService.Mode;
        try
        {
            LocalizationService.Apply(LanguageMode.English);
            var viewModel = new MainWindowViewModel();
            var changedProperties = new List<string?>();
            viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

            LocalizationService.Apply(LanguageMode.SimplifiedChinese);

            Assert.Equal("FFDecsaSharp v0.0.2", viewModel.AppTitle);
            Assert.Equal("详情", viewModel.DetailPaneButtonText);
            Assert.Equal("就绪", viewModel.StatusText);
            Assert.Contains(nameof(MainWindowViewModel.DetailPaneButtonText), changedProperties);
            Assert.Contains(nameof(MainWindowViewModel.AppTitle), changedProperties);
            Assert.Contains(nameof(MainWindowViewModel.QueueSummary), changedProperties);
            Assert.Contains(nameof(MainWindowViewModel.StatusText), changedProperties);
        }
        finally
        {
            LocalizationService.Apply(originalLanguage);
        }
    }

    private static DecryptionTask CreateTask(string fileName) => new(
        [Path.Combine(Path.GetTempPath(), fileName)],
        Path.Combine(Path.GetTempPath(), $"output-{fileName}"),
        "invalid",
        "invalid",
        0,
        0);
}
