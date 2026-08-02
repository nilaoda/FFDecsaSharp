using FFDecsaSharp.Gui;

namespace FFDecsaSharp.Gui.Services;

internal static class AppVersion
{
    public static string Current { get; } = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
