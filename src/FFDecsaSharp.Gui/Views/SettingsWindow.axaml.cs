using Avalonia.Interactivity;
using FFDecsaSharp.Gui.Services;
using FFDecsaSharp.Gui.ViewModels;

namespace FFDecsaSharp.Gui.Views;

public partial class SettingsWindow : ShadUI.Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly LanguageMode _originalLanguage;

    public SettingsWindow()
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel();
        _originalLanguage = _viewModel.GetOriginalLanguage();
        DataContext = _viewModel;
    }

    private void Save_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        LocalizationService.Apply(_originalLanguage);
        Close(false);
    }
}
