using Core.Events;

namespace Cli.Notifications;

internal static class NotificationFormatter
{
    internal static string Format(OrderChangedEventArgs change) =>
        $"{change.At:HH:mm:ss} {change.Kind,-16} {change.OrderId} → рядків {change.LinesCount}, сума {change.Total:F2}, стан {change.Status}";
}
