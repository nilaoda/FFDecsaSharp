namespace FFDecsaSharp.Gui.Helpers;

internal static class ThroughputFormatter
{
    private const double BytesPerMegabyte = 1024d * 1024d;

    public const string Zero = "0.00 MB/s";

    public static string Format(double bytesPerSecond) => $"{Math.Max(0, bytesPerSecond) / BytesPerMegabyte:0.00} MB/s";
}
