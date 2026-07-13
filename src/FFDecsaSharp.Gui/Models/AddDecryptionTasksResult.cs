namespace FFDecsaSharp.Gui.Models;

public sealed class AddDecryptionTasksResult
{
    public required IReadOnlyList<string> InputPaths { get; init; }
    public required string EvenKey { get; init; }
    public required string OddKey { get; init; }
    public required string OutputFilePath { get; init; }
    public long PacketOffset { get; init; }
    public long PacketLimit { get; init; }
}
