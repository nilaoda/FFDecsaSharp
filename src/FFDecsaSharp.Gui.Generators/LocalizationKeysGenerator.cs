using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace FFDecsaSharp.Gui.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class LocalizationKeysGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MissingKey = Descriptor("FDGUI001", "Localization key missing from resource file", "Localization key '{0}' exists in '{1}' but is missing from '{2}'.");
    private static readonly DiagnosticDescriptor IdentifierCollision = Descriptor("FDGUI002", "Localization key identifier collision", "Localization keys generate the same C# identifier '{0}': {1}.");
    private static readonly DiagnosticDescriptor PlaceholderMismatch = Descriptor("FDGUI003", "Localization format placeholder mismatch", "Localization key '{0}' has placeholders {{{1}}} in '{2}' but {{{3}}} in '{4}'.");
    private static readonly DiagnosticDescriptor InvalidResx = Descriptor("FDGUI004", "Localization resource file is invalid", "Localization resource file '{0}' could not be parsed: {1}.");
    private static readonly DiagnosticDescriptor MissingLanguage = Descriptor("FDGUI005", "Localization language resource file is missing", "Localization resource file '{0}' is missing.");
    private static readonly DiagnosticDescriptor DuplicateKey = Descriptor("FDGUI006", "Localization key is duplicated", "Localization key '{0}' is duplicated in '{1}'.");
    private static readonly DiagnosticDescriptor MissingDynamicResource = Descriptor("FDGUI007", "Localized DynamicResource key is missing", "Localized DynamicResource key '{0}' in '{1}' does not exist in localization resources.");
    private static readonly Language[] Languages = [new("ZhHans", "Strings.zh-Hans.resx"), new("En", "Strings.en.resx"), new("ZhHant", "Strings.zh-Hant.resx")];
    private static readonly Regex PlaceholderRegex = new(@"\{(\d+)(?:[^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex DynamicResourceRegex = new(@"\{DynamicResource\s+([A-Za-z0-9_.]+)\}", RegexOptions.Compiled);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider.Select(static (file, token) => new SourceFile(file.Path, file.GetText(token)?.ToString() ?? ""));
        context.RegisterSourceOutput(files.Collect(), Generate);
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<SourceFile> sources)
    {
        var resourceFiles = sources.Where(static source => source.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)).ToArray();
        var axamlFiles = sources.Where(static source => source.Path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)).ToArray();
        var dictionaries = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var language in Languages)
        {
            var source = resourceFiles.FirstOrDefault(file => string.Equals(Path.GetFileName(file.Path), language.FileName, StringComparison.OrdinalIgnoreCase));
            if (source == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(MissingLanguage, Location.None, language.FileName));
                continue;
            }

            if (TryParse(context, source, out var entries))
            {
                dictionaries[language.Name] = entries;
                paths[language.Name] = source.Path;
            }
        }

        if (!dictionaries.TryGetValue("En", out var baseline)) return;
        foreach (var pair in dictionaries)
        {
            foreach (var key in baseline.Keys.Where(key => !pair.Value.ContainsKey(key))) context.ReportDiagnostic(Diagnostic.Create(MissingKey, Location.None, key, "Strings.en.resx", Path.GetFileName(paths[pair.Key])));
            foreach (var key in pair.Value.Keys.Where(key => !baseline.ContainsKey(key))) context.ReportDiagnostic(Diagnostic.Create(MissingKey, Location.None, key, Path.GetFileName(paths[pair.Key]), "Strings.en.resx"));
        }

        var keys = baseline.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
        var collisions = keys.GroupBy(ToIdentifier, StringComparer.Ordinal).Where(static group => group.Count() > 1).ToArray();
        foreach (var collision in collisions) context.ReportDiagnostic(Diagnostic.Create(IdentifierCollision, Location.None, collision.Key, string.Join(", ", collision)));
        if (collisions.Length > 0) return;

        foreach (var key in keys)
        {
            var expected = PlaceholderIndexes(baseline[key]);
            foreach (var pair in dictionaries)
            {
                if (!pair.Value.TryGetValue(key, out var translated)) continue;
                var actual = PlaceholderIndexes(translated);
                if (!expected.SequenceEqual(actual)) context.ReportDiagnostic(Diagnostic.Create(PlaceholderMismatch, Location.None, key, Format(expected), "Strings.en.resx", Format(actual), Path.GetFileName(paths[pair.Key])));
            }
        }

        var knownKeys = new HashSet<string>(keys, StringComparer.Ordinal);
        foreach (var source in axamlFiles)
        {
            foreach (Match match in DynamicResourceRegex.Matches(source.Content))
            {
                var key = match.Groups[1].Value;
                if (key.Contains(".", StringComparison.Ordinal) && !knownKeys.Contains(key)) context.ReportDiagnostic(Diagnostic.Create(MissingDynamicResource, Location.None, key, Path.GetFileName(source.Path)));
            }
        }

        context.AddSource("LocalizationKeys.g.cs", SourceText.From(Render(keys, dictionaries), Encoding.UTF8));
    }

    private static bool TryParse(SourceProductionContext context, SourceFile source, out Dictionary<string, string> entries)
    {
        entries = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var document = XDocument.Parse(source.Content, LoadOptions.PreserveWhitespace);
            foreach (var data in document.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
            {
                var key = data.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(key)) continue;
                var resourceKey = key!;
                if (entries.ContainsKey(resourceKey)) context.ReportDiagnostic(Diagnostic.Create(DuplicateKey, Location.None, resourceKey, Path.GetFileName(source.Path)));
                else entries.Add(resourceKey, data.Element("value")?.Value ?? "");
            }
            return true;
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidResx, Location.None, Path.GetFileName(source.Path), ex.Message));
            return false;
        }
    }

    private static string Render(IReadOnlyList<string> keys, IReadOnlyDictionary<string, Dictionary<string, string>> dictionaries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace FFDecsaSharp.Gui;");
        builder.AppendLine("public static class LocKeys { ");
        foreach (var key in keys) builder.Append("public const string ").Append(ToIdentifier(key)).Append(" = ").Append(Literal(key)).AppendLine(";");
        builder.AppendLine("}");
        builder.AppendLine("public static class L { ");
        foreach (var key in keys)
        {
            var identifier = ToIdentifier(key);
            var placeholders = PlaceholderIndexes(dictionaries["En"][key]);
            if (placeholders.Length == 0) builder.Append("public static string ").Append(identifier).Append(" => FFDecsaSharp.Gui.Services.LocalizationService.Get(LocKeys.").Append(identifier).AppendLine(");");
            else
            {
                builder.Append("public static string ").Append(identifier).Append("(");
                for (var index = 0; index <= placeholders.Max(); index++) { if (index > 0) builder.Append(", "); builder.Append("object? arg").Append(index); }
                builder.Append(") => FFDecsaSharp.Gui.Services.LocalizationService.Format(LocKeys.").Append(identifier);
                for (var index = 0; index <= placeholders.Max(); index++) builder.Append(", arg").Append(index);
                builder.AppendLine(");");
            }
        }
        builder.AppendLine("}");
        builder.AppendLine("internal static class LocalizationCatalog { ");
        foreach (var language in Languages)
        {
            builder.Append("internal static global::System.Collections.Generic.IReadOnlyDictionary<string, string> ").Append(language.Name).AppendLine(" { get; } = new global::System.Collections.Generic.Dictionary<string, string>(global::System.StringComparer.Ordinal) {");
            if (dictionaries.TryGetValue(language.Name, out var entries)) foreach (var pair in entries.OrderBy(static entry => entry.Key, StringComparer.Ordinal)) builder.Append("[").Append(Literal(pair.Key)).Append("] = ").Append(Literal(pair.Value)).AppendLine(",");
            builder.AppendLine("};");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static DiagnosticDescriptor Descriptor(string id, string title, string message) => new(id, title, message, "Localization", DiagnosticSeverity.Error, true);
    private static int[] PlaceholderIndexes(string value) => PlaceholderRegex.Matches(value).Cast<Match>().Select(static match => int.Parse(match.Groups[1].Value)).Distinct().OrderBy(static index => index).ToArray();
    private static string Format(IReadOnlyList<int> values) => string.Join(", ", values);
    private static string ToIdentifier(string key)
    {
        var result = new string(key.Select(static c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        if (result.Length == 0 || char.IsDigit(result[0])) result = "_" + result;
        return SyntaxFacts.GetKeywordKind(result) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(result) != SyntaxKind.None ? result + "_" : result;
    }
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
    private sealed class SourceFile
    {
        public SourceFile(string path, string content) { Path = path; Content = content; }
        public string Path { get; }
        public string Content { get; }
    }

    private sealed class Language
    {
        public Language(string name, string fileName) { Name = name; FileName = fileName; }
        public string Name { get; }
        public string FileName { get; }
    }
}
