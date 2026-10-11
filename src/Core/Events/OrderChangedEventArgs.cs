using Core.Domain;

namespace Core.Events;

public sealed class OrderChangedEventArgs(
    string orderId, OrderChangeKind kind, int linesCount, decimal total,
    OrderStatus status) : EventArgs
{
    public string OrderId { get; } = orderId;
    public OrderChangeKind Kind { get; } = kind;
    public int LinesCount { get; } = linesCount;
    public decimal Total { get; } = total;
    public OrderStatus Status { get; } = status;
    public DateTimeOffset At { get; } = DateTimeOffset.Now;
}
