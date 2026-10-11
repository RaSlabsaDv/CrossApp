using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Services;

public sealed class OrderService
{
    public const int MaxConfirmedOrdersPerCustomer = 5;

    private readonly IOrderStore _store;

    public OrderService(IOrderStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public Order CreateOrder(CustomerDto customer, string? id = null)
    {
        Order order = Order.Create(id ?? $"ORD-{Guid.NewGuid():N}", customer);
        _store.Add(order);
        return order;
    }

    public void AddLine(string id, string productId, string name, decimal price,
        int quantity, string? sku = null, string? note = null)
    {
        Order order = RequireOrder(id);
        order.AddLine(productId, name, price, quantity, sku, note);
        _store.Update(order);
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
    }

    public void Cancel(string id)
    {
        Order order = RequireOrder(id);
        order.Cancel();
        _store.Update(order);
    }

    public IReadOnlyList<Order> All() => _store.List();
    public Order? Find(string id) => _store.GetById(id);
    public bool Remove(string id) => _store.Remove(id);

    public IReadOnlyList<Order> Search(Func<Order, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.List().Where(predicate).ToList().AsReadOnly();
    }

    private Order RequireOrder(string id) => _store.GetById(id)
        ?? throw new InvalidOperationException($"Order with ID {id} does not exist");
}
