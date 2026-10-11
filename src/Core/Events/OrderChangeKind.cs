namespace Core.Events;

public enum OrderChangeKind
{
    OrderCreated,
    LineAdded,
    OrderConfirmed,
    OrderCancelled,
    OrderRemoved
}
