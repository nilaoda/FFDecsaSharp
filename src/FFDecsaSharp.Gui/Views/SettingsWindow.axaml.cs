using Avalonia.Interactivity;
using FFDecsaSharp.Gui.Services;
using FFDecsaSharp.Gui.ViewModels;

namespace FFDecsaSharp.Gui.Views;

public partial class SettingsWindow : ShadUI.Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel();
        DataContext = _viewModel;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.TrySave(out Exception? exception))
        {
            _viewModel.ApplySavedAppearance();
            Close(true);
            return;
        }

        _viewModel.StatusText = exception?.Message ?? "Unable to save settings.";
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
