namespace FFDecsaSharp.Gui.Helpers;

internal static class ThroughputFormatter
{
    private const double BytesPerMegabyte = 1024d * 1024d;

    public const string Zero = "0.00 MB/s";

    public static string Format(double bytesPerSecond) => $"{Math.Max(0, bytesPerSecond) / BytesPerMegabyte:0.00} MB/s";

    public static string FormatFileSize(long bytes)
    {
        const double bytesPerKilobyte = 1024d;
        const double bytesPerGigabyte = 1024d * 1024d * 1024d;
        if (bytes >= bytesPerGigabyte) return $"{bytes / bytesPerGigabyte:0.00} GB";
        if (bytes >= BytesPerMegabyte) return $"{bytes / BytesPerMegabyte:0.00} MB";
        if (bytes >= bytesPerKilobyte) return $"{bytes / bytesPerKilobyte:0.00} KB";
        return $"{Math.Max(0, bytes)} B";
    }
}
