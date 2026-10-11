using Core.Abstractions;
using Core.Domain;
using Core.Dto;
using Core.Events;

namespace Core.Services;

public sealed class OrderService
{
    public const int MaxConfirmedOrdersPerCustomer = 5;

    private readonly IOrderStore _store;
    private readonly INotificationSink? _notificationSink;

    public event EventHandler<OrderChangedEventArgs>? Changed;

    public OrderService(IOrderStore store, INotificationSink? notificationSink = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _notificationSink = notificationSink;
    }

    public Order CreateOrder(CustomerDto customer, string? id = null)
    {
        Order order = Order.Create(id ?? $"ORD-{Guid.NewGuid():N}", customer);
        _store.Add(order);
        OnChanged(order, OrderChangeKind.OrderCreated);
        return order;
    }

    public void AddLine(string id, string productId, string name, decimal price,
        int quantity, string? sku = null, string? note = null)
    {
        Order order = RequireOrder(id);
        order.AddLine(productId, name, price, quantity, sku, note);
        _store.Update(order);
        OnChanged(order, OrderChangeKind.LineAdded);
    }

    public void Confirm(string id)
    {
        Order order = RequireOrder(id);
        if (order.Status != OrderStatus.Draft)
            throw new InvalidOperationException($"Order {order.Id} must be a draft before confirmation");

        int confirmedCount = _store.List()
            .Count(existing => string.Equals(existing.CustomerId, order.CustomerId,
                StringComparison.OrdinalIgnoreCase) && existing.IsConfirmed);
        if (confirmedCount >= MaxConfirmedOrdersPerCustomer)
            throw new InvalidOperationException(
                $"Customer {order.CustomerId} already has {confirmedCount} confirmed orders; the limit is {MaxConfirmedOrdersPerCustomer}");

        order.Confirm();
        _store.Update(order);
        OnChanged(order, OrderChangeKind.OrderConfirmed);
    }

    public void Cancel(string id)
    {
        Order order = RequireOrder(id);
        order.Cancel();
        _store.Update(order);
        OnChanged(order, OrderChangeKind.OrderCancelled);
    }

    public IReadOnlyList<Order> All() => _store.List();
    public Order? Find(string id) => _store.GetById(id);
    public bool Remove(string id)
    {
        Order? order = _store.GetById(id);
        if (order is null || !_store.Remove(id))
            return false;

        OnChanged(order, OrderChangeKind.OrderRemoved);
        return true;
    }

    public IReadOnlyList<Order> FindAll(Func<Order, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.List().Where(predicate).ToList().AsReadOnly();
    }

    public IReadOnlyList<Order> Search(Func<Order, bool> predicate)
    {
        return FindAll(predicate);
    }

    private void OnChanged(Order order, OrderChangeKind kind)
    {
        var change = new OrderChangedEventArgs(
            order.Id, kind, order.Lines.Count, order.Total, order.Status);
        _notificationSink?.Notify(change);
        Changed?.Invoke(this, change);
    }

    private Order RequireOrder(string id) => _store.GetById(id)
        ?? throw new InvalidOperationException($"Order with ID {id} does not exist");
}
