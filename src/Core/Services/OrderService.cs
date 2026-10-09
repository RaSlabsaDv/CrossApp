using Core.Domain;

namespace Core.Services;

public static class OrderService
{
    public const int MaxConfirmedOrdersPerCustomer = 5;

    public static void Confirm(Order order, IReadOnlyCollection<Order> existingOrders)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(existingOrders);
        if (existingOrders.Any(existing => existing is null))
            throw new ArgumentException("Existing orders cannot contain null entries", nameof(existingOrders));
        if (order.Status != OrderStatus.Draft)
            throw new InvalidOperationException($"Order {order.Id} must be a draft before confirmation");

        int confirmedCount = existingOrders
            .Where(existing => existing.CustomerId == order.CustomerId && existing.IsConfirmed)
            .Select(existing => existing.Id)
            .Distinct()
            .Count();
        if (confirmedCount >= MaxConfirmedOrdersPerCustomer)
            throw new InvalidOperationException(
                $"Customer {order.CustomerId} already has {confirmedCount} confirmed orders; the limit is {MaxConfirmedOrdersPerCustomer}");

        order.Confirm();
    }
}
