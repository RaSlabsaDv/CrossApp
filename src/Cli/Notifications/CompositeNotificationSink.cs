using Core.Abstractions;
using Core.Events;

namespace Cli.Notifications;

public sealed class CompositeNotificationSink(params INotificationSink[] sinks) : INotificationSink
{
    private readonly INotificationSink[] _sinks = Validate(sinks);

    public void Notify(OrderChangedEventArgs change)
    {
        foreach (INotificationSink sink in _sinks)
            sink.Notify(change);
    }

    private static INotificationSink[] Validate(INotificationSink[] sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        if (sinks.Any(sink => sink is null))
            throw new ArgumentException("Notification sinks cannot contain null", nameof(sinks));
        return sinks.ToArray();
    }
}
