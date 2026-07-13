using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFDecsaSharp.Gui.Helpers;
using FFDecsaSharp.Gui.Models;
using FFDecsaSharp.Gui.Services;

namespace FFDecsaSharp.Gui.ViewModels;

public partial class TaskEditorViewModel : ViewModelBase
{
    [ObservableProperty] private string _inputFilesText = "";
    [ObservableProperty] private string _outputFilePath = "";
    [ObservableProperty] private string _controlWord = "";
    [ObservableProperty] private string _evenKey = "";
    [ObservableProperty] private string _oddKey = "";
    [ObservableProperty] private string _packetOffset = "0";
    [ObservableProperty] private string _packetLimit = "0";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _useSeparateKeys;

    public string InputSelectionSummary => L.App_SelectedFiles(GetInputPaths().Length);
    public bool CanQueueEditor => GetInputPaths().Length > 0
        && ControlWordParser.TryParse(EffectiveEvenKey, out _)
        && ControlWordParser.TryParse(EffectiveOddKey, out _)
        && TryParsePacketCount(PacketOffset, out _)
        && TryParsePacketCount(PacketLimit, out _);

    public AddDecryptionTasksResult? Result { get; private set; }

    private string EffectiveEvenKey => UseSeparateKeys ? EvenKey : ControlWord;
    private string EffectiveOddKey => UseSeparateKeys ? OddKey : ControlWord;

    public void SetInputFiles(IEnumerable<string> inputPaths)
    {
        string[] paths = inputPaths.Where(static path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        InputFilesText = string.Join("; ", paths);
        if (paths.Length > 0) OutputFilePath = GetDefaultOutputPath(paths[0]);
    }

    public bool TryCreateTask()
    {
        string[] inputPaths = GetInputPaths();
        if (inputPaths.Length == 0)
        {
            StatusText = L.Error_NoInputFiles;
            return false;
        }
        if (!ControlWordParser.TryParse(EffectiveEvenKey, out _) || !ControlWordParser.TryParse(EffectiveOddKey, out _))
        {
            StatusText = L.Error_InvalidKeys;
            return false;
        }
        if (!TryParsePacketCount(PacketOffset, out long packetOffset) || !TryParsePacketCount(PacketLimit, out long packetLimit))
        {
            StatusText = L.Error_InvalidPacketRange;
            return false;
        }

        string outputPath = string.IsNullOrWhiteSpace(OutputFilePath) ? GetDefaultOutputPath(inputPaths[0]) : Path.GetFullPath(OutputFilePath);
        if (inputPaths.Any(path => string.Equals(path, outputPath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = L.Error_OutputMatchesInput;
            return false;
        }

        Result = new AddDecryptionTasksResult
        {
            InputPaths = inputPaths,
            EvenKey = EffectiveEvenKey.ToUpperInvariant(),
            OddKey = EffectiveOddKey.ToUpperInvariant(),
            OutputFilePath = outputPath,
            PacketOffset = packetOffset,
            PacketLimit = packetLimit,
        };
        return true;
    }

    partial void OnInputFilesTextChanged(string value)
    {
        OnPropertyChanged(nameof(InputSelectionSummary));
        OnPropertyChanged(nameof(CanQueueEditor));
    }
    partial void OnControlWordChanged(string value) { OnPropertyChanged(nameof(CanQueueEditor)); if (!UseSeparateKeys) { EvenKey = value; OddKey = value; } }
    partial void OnEvenKeyChanged(string value) => OnPropertyChanged(nameof(CanQueueEditor));
    partial void OnOddKeyChanged(string value) => OnPropertyChanged(nameof(CanQueueEditor));
    partial void OnPacketOffsetChanged(string value) => OnPropertyChanged(nameof(CanQueueEditor));
    partial void OnPacketLimitChanged(string value) => OnPropertyChanged(nameof(CanQueueEditor));
    partial void OnUseSeparateKeysChanged(bool value)
    {
        if (!value) { EvenKey = ControlWord; OddKey = ControlWord; }
        OnPropertyChanged(nameof(CanQueueEditor));
    }

    private string[] GetInputPaths() => InputFilesText.Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string GetDefaultOutputPath(string inputPath) => Path.Combine(
        Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory,
        Path.GetFileNameWithoutExtension(inputPath) + "_dec" + Path.GetExtension(inputPath));

    private static bool TryParsePacketCount(string? text, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return true;
        text = text.Trim();
        try
        {
            value = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToInt64(text[2..], 16) : Convert.ToInt64(text);
            return value >= 0;
        }
        catch (FormatException) { return false; }
        catch (OverflowException) { return false; }
    }
}
