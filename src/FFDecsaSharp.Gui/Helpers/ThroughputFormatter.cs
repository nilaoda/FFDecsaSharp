namespace FFDecsaSharp.Gui.Helpers;

internal static class ThroughputFormatter
{
    private const double BytesPerMegabyte = 1024d * 1024d;

    public const string Zero = "0.00 MB/s";

    public static string Format(double bytesPerSecond) => $"{Math.Max(0, bytesPerSecond) / BytesPerMegabyte:0.00} MB/s";

    public static string FormatFileSize(long bytes)
    {
        const double bytesPerKibibyte = 1024d;
        const double bytesPerGibibyte = 1024d * 1024d * 1024d;
        if (bytes >= bytesPerGibibyte) return $"{bytes / bytesPerGibibyte:0.00} GiB";
        if (bytes >= BytesPerMegabyte) return $"{bytes / BytesPerMegabyte:0.00} MiB";
        if (bytes >= bytesPerKibibyte) return $"{bytes / bytesPerKibibyte:0.00} KiB";
        return $"{Math.Max(0, bytes)} B";
    }
}
