using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FFDecsaSharp.Gui.Services;
using FFDecsaSharp.Gui.ViewModels;
using FFDecsaSharp.Gui.Views;

namespace FFDecsaSharp.Gui;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        TextBoxContextMenuService.Install();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
                    System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {e.Exception}");
            });
            e.SetObserved();
        };

        AppSettingsService.Load();
        LocalizationService.Apply(LanguageMode.Auto);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
        base.OnFrameworkInitializationCompleted();
    }
}

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
