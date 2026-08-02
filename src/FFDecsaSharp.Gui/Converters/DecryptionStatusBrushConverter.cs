using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FFDecsaSharp.Gui.Converters;

public sealed class DecryptionStatusBrushConverter : IValueConverter
{
    private static readonly IBrush QueuedBackground = Brush.Parse("#FEF3C7");
    private static readonly IBrush QueuedForeground = Brush.Parse("#92400E");
    private static readonly IBrush RunningBackground = Brush.Parse("#DBEAFE");
    private static readonly IBrush RunningForeground = Brush.Parse("#1D4ED8");
    private static readonly IBrush CompletedBackground = Brush.Parse("#DCFCE7");
    private static readonly IBrush CompletedForeground = Brush.Parse("#166534");
    private static readonly IBrush FailedBackground = Brush.Parse("#FEE2E2");
    private static readonly IBrush FailedForeground = Brush.Parse("#991B1B");
    private static readonly IBrush CanceledBackground = Brush.Parse("#E2E8F0");
    private static readonly IBrush CanceledForeground = Brush.Parse("#475569");
    private static readonly IBrush StoppedBackground = Brush.Parse("#F1F5F9");
    private static readonly IBrush StoppedForeground = Brush.Parse("#334155");

    public string Variant { get; set; } = "Background";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool foreground = string.Equals(Variant, "Foreground", StringComparison.OrdinalIgnoreCase);
        string? status = value as string;
        return status switch
        {
            LocKeys.Status_Queued => foreground ? QueuedForeground : QueuedBackground,
            LocKeys.Status_Running => foreground ? RunningForeground : RunningBackground,
            LocKeys.Status_Completed => foreground ? CompletedForeground : CompletedBackground,
            LocKeys.Status_Failed => foreground ? FailedForeground : FailedBackground,
            LocKeys.Status_Stopped => foreground ? StoppedForeground : StoppedBackground,
            _ => foreground ? CanceledForeground : CanceledBackground,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
