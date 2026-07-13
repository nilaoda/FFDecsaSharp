using CommunityToolkit.Mvvm.ComponentModel;
using FFDecsaSharp.Gui.Helpers;
using FFDecsaSharp.Gui.Services;

namespace FFDecsaSharp.Gui.Models;

public partial class DecryptionTask : ObservableObject
{
    private static int _nextId;

    [ObservableProperty] private string _outputPath;
    [ObservableProperty] private string _statusKey = LocKeys.Status_Queued;
    [ObservableProperty] private string _status = LocalizationService.Get(LocKeys.Status_Queued);
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _packetCount = "-";
    [ObservableProperty] private string _decryptedCount = "-";
    [ObservableProperty] private string _elapsed = "-";
    [ObservableProperty] private string _eta = "-";
    [ObservableProperty] private string _speed = ThroughputFormatter.Zero;
    [ObservableProperty] private double _rawSpeed;
    [ObservableProperty] private string _log = "";
    [ObservableProperty] private bool _isRunning;

    public DecryptionTask(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        string evenKey,
        string oddKey,
        long packetOffset,
        long packetLimit)
    {
        Id = Interlocked.Increment(ref _nextId);
        InputPaths = inputPaths.Where(static path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (InputPaths.Count == 0) throw new ArgumentException("At least one input path is required.", nameof(inputPaths));
        _outputPath = outputPath;
        EvenKey = evenKey;
        OddKey = oddKey;
        PacketOffset = packetOffset;
        PacketLimit = packetLimit;
    }

    public int Id { get; }
    public IReadOnlyList<string> InputPaths { get; }
    public string FileName => InputPaths.Count == 1
        ? Path.GetFileName(InputPaths[0])
        : $"{Path.GetFileName(InputPaths[0])} + {InputPaths.Count - 1}";
    public string EvenKey { get; }
    public string OddKey { get; }
    public string ControlWord => string.Equals(EvenKey, OddKey, StringComparison.Ordinal) ? EvenKey : $"{EvenKey} / {OddKey}";
    public long PacketOffset { get; }
    public long PacketLimit { get; }
    public string PacketRange => PacketLimit == 0
        ? $"{PacketOffset} -"
        : $"{PacketOffset} - {PacketOffset + PacketLimit - 1}";

    public void SetStatus(string resourceKey)
    {
        StatusKey = resourceKey;
        Status = LocalizationService.Get(resourceKey);
    }

    public void RefreshLocalizedText() => Status = LocalizationService.Get(StatusKey);
}
