using Avalonia;
using Avalonia.Styling;
using FFDecsaSharp.Gui.Models;

namespace FFDecsaSharp.Gui.Services;

internal static class AppThemeService
{
    public static void Apply(AppThemeMode mode)
    {
        if (Application.Current is not { } application)
        {
            return;
        }

        application.RequestedThemeVariant = mode switch
        {
            AppThemeMode.Light => ThemeVariant.Light,
            AppThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
