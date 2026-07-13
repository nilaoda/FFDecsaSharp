using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FFDecsaSharp.Gui.Models;
using FFDecsaSharp.Gui.Services;
using FFDecsaSharp.Gui.ViewModels;

namespace FFDecsaSharp.Gui.Views;

public partial class TaskEditorWindow : ShadUI.Window
{
    private readonly TaskEditorViewModel _viewModel;

    public TaskEditorWindow()
    {
        InitializeComponent();
        _viewModel = new TaskEditorViewModel();
        DataContext = _viewModel;
    }

    public AddDecryptionTasksResult? Result => _viewModel.Result;

    public void SetInputFiles(IEnumerable<string> paths) => _viewModel.SetInputFiles(paths);

    private async void SelectInputFiles_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Get(LocKeys.App_Input),
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("MPEG transport stream") { Patterns = ["*.ts", "*.m2ts", "*.mts"] }],
        });
        _viewModel.SetInputFiles(files.Select(file => file.Path.LocalPath));
    }

    private async void SelectOutputFile_Click(object? sender, RoutedEventArgs e)
    {
        string firstInput = _viewModel.InputFilesText.Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "output.ts";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationService.Get(LocKeys.App_Output),
            SuggestedFileName = string.IsNullOrWhiteSpace(_viewModel.OutputFilePath)
                ? Path.GetFileNameWithoutExtension(firstInput) + "_dec" + Path.GetExtension(firstInput)
                : Path.GetFileName(_viewModel.OutputFilePath),
            DefaultExtension = Path.GetExtension(firstInput).TrimStart('.'),
            FileTypeChoices = [new FilePickerFileType("MPEG transport stream") { Patterns = ["*.ts", "*.m2ts", "*.mts"] }],
            ShowOverwritePrompt = true,
        });
        if (file is not null) _viewModel.OutputFilePath = file.Path.LocalPath;
    }

    private void Queue_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.TryCreateTask()) Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
