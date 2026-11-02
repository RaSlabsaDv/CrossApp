using Core.Domain;
using Core.Dto;

namespace Core.Tests;

internal static class TestData
{
    public static CustomerDto Customer => new("C-TEST", "Тест", "test@example.com");
    public static Order Order(string id = "ORD-A", decimal price = 10m, int quantity = 2)
    {
        var order = Core.Domain.Order.Create(id, Customer);
        order.AddLine("P-1", "Кабель", price, quantity);
        return order;
    }
    public static IReadOnlyList<Order> Items() =>
        [Order("ORD-B", 50m), Order("ORD-A", 50m), Order("ORD-C", 5m)];
}
