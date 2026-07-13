namespace FFDecsaSharp.Gui.Services;

internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FFDecsaSharp");

    public static string SettingsFile { get; } = Path.Combine(DataDirectory, "settings.json");
}
