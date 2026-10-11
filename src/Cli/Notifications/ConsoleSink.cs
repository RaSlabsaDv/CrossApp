using Core.Abstractions;
using Core.Events;

namespace Cli.Notifications;

public sealed class ConsoleSink : INotificationSink
{
    public void Notify(OrderChangedEventArgs change) =>
        Console.WriteLine(NotificationFormatter.Format(change));
}
