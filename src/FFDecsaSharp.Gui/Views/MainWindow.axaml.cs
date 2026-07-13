using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FFDecsaSharp.Gui.Models;
using FFDecsaSharp.Gui.Services;
using FFDecsaSharp.Gui.ViewModels;

namespace FFDecsaSharp.Gui.Views;

public partial class MainWindow : ShadUI.Window
{
    private bool _forceClose;

    public MainWindow() => InitializeComponent();

    private async void NewTask_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        var editor = new TaskEditorWindow();
        bool? result = await editor.ShowDialog<bool?>(this);
        if (result == true && editor.Result is AddDecryptionTasksResult taskResult)
        {
            var task = new DecryptionTask(
                taskResult.InputPaths,
                taskResult.OutputFilePath,
                taskResult.EvenKey,
                taskResult.OddKey,
                taskResult.PacketOffset,
                taskResult.PacketLimit);
            viewModel.Tasks.Add(task);
            viewModel.SelectedTask = task;
        }
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        await new SettingsWindow().ShowDialog<bool?>(this);
    }

    private void MainWindow_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void MainWindow_Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        var files = e.DataTransfer.TryGetFiles();
        if (files is null) return;
        var paths = files
            .OfType<IStorageFile>()
            .Select(f => f.Path.LocalPath)
            .Where(p => p.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(".m2ts", StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(".mts", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (paths.Length == 0) return;

        var editor = new TaskEditorWindow();
        editor.SetInputFiles(paths);
        bool? result = await editor.ShowDialog<bool?>(this);
        if (result == true && editor.Result is AddDecryptionTasksResult taskResult)
        {
            var task = new DecryptionTask(
                taskResult.InputPaths,
                taskResult.OutputFilePath,
                taskResult.EvenKey,
                taskResult.OddKey,
                taskResult.PacketOffset,
                taskResult.PacketLimit);
            viewModel.Tasks.Add(task);
            viewModel.SelectedTask = task;
        }
    }

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose) return;
        if (DataContext is not MainWindowViewModel { HasRunningTasks: true }) return;

        e.Cancel = true;
        var dialog = new ConfirmDialog(
            L.App_RunningTasksCloseConfirm,
            L.App_Yes,
            L.App_No);
        var answer = await dialog.ShowDialog<bool?>(this);
        if (answer == true)
        {
            _forceClose = true;
            Close();
        }
    }
}
