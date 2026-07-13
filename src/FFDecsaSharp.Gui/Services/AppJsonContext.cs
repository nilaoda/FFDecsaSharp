using System.Text.Json.Serialization;
using FFDecsaSharp.Gui.Models;

namespace FFDecsaSharp.Gui.Services;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppJsonContext : JsonSerializerContext;
