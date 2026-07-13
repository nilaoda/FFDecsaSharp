using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Avalonia;

namespace FFDecsaSharp.Gui.Services;

public enum LanguageMode
{
    Auto,
    English,
    SimplifiedChinese,
    TraditionalChinese
}

internal static class LocalizationService
{
    private static LanguageMode _mode = LanguageMode.Auto;

    public static event EventHandler? LanguageChanged;
    public static LanguageMode Mode => _mode;
    public static LanguageMode EffectiveMode { get; private set; } = Resolve(LanguageMode.Auto);

    public static void ApplyForCommandLine(LanguageMode mode)
    {
        _mode = mode;
        EffectiveMode = Resolve(mode);
    }

    public static void Apply(LanguageMode mode)
    {
        ApplyForCommandLine(mode);
        var resources = GetResources(EffectiveMode);
        if (Application.Current != null)
        {
            foreach (var (key, value) in resources)
                Application.Current.Resources[key] = value;
        }
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        var resources = GetResources(EffectiveMode);
        return resources.TryGetValue(key, out var value) ? value : key;
    }

    public static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);

    private static LanguageMode Resolve(LanguageMode mode)
    {
        if (mode != LanguageMode.Auto) return mode;
        foreach (var name in GetPreferredLanguageNames())
        {
            var resolved = ResolveLanguageName(name);
            if (resolved.HasValue) return resolved.Value;
        }
        return LanguageMode.English;
    }

    private static LanguageMode? ResolveLanguageName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = NormalizeLanguageName(name);
        if (normalized.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) || normalized.Contains("-TW", StringComparison.OrdinalIgnoreCase) || normalized.Contains("-HK", StringComparison.OrdinalIgnoreCase) || normalized.Contains("-MO", StringComparison.OrdinalIgnoreCase)) return LanguageMode.TraditionalChinese;
        if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return LanguageMode.SimplifiedChinese;
        return normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? LanguageMode.English : null;
    }

    private static IEnumerable<string> GetPreferredLanguageNames()
    {
        if (OperatingSystem.IsMacOS()) foreach (var name in ReadMacOSAppleLanguages()) yield return name;
        yield return CultureInfo.CurrentUICulture.Name;
        yield return CultureInfo.CurrentCulture.Name;
        foreach (var variable in new[] { "LC_ALL", "LC_MESSAGES", "LANG" }) yield return Environment.GetEnvironmentVariable(variable) ?? "";
    }

    [SupportedOSPlatform("macos")]
    private static IReadOnlyList<string> ReadMacOSAppleLanguages()
    {
        try
        {
            var startInfo = new ProcessStartInfo { FileName = "/usr/bin/defaults", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            startInfo.ArgumentList.Add("read"); startInfo.ArgumentList.Add("-g"); startInfo.ArgumentList.Add("AppleLanguages");
            using var process = Process.Start(startInfo);
            if (process == null || !process.WaitForExit(1000)) return [];
            return process.StandardOutput.ReadToEnd().Split('\n').Select(static line => line.Trim().Trim(',', '"')).Where(static name => name.Length > 0 && name is not "(" and not ")").ToArray();
        }
        catch { return []; }
    }

    private static string NormalizeLanguageName(string name)
    {
        var normalized = name.Trim().Trim('"').Replace('_', '-');
        var dotIndex = normalized.IndexOf('.', StringComparison.Ordinal);
        return dotIndex >= 0 ? normalized[..dotIndex] : normalized;
    }

    private static IReadOnlyDictionary<string, string> GetResources(LanguageMode mode) => mode switch
    {
        LanguageMode.English => LocalizationCatalog.En,
        LanguageMode.TraditionalChinese => LocalizationCatalog.ZhHant,
        _ => LocalizationCatalog.ZhHans
    };
}
