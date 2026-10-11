using Core.Domain;
using Core.Dto;

namespace Core;

public static class SampleData
{
    public static IReadOnlyList<CustomerDto> Customers() => Enumerable.Range(1, 7)
        .Select(number => new CustomerDto($"C-{number:D3}", $"Клієнт {number}",
            $"customer{number}@example.com")).ToList().AsReadOnly();

    public static IReadOnlyList<Order> Orders()
    {
        // 18 orders, six customers and all three statuses; the seventh customer has no orders.
        var customers = Customers();
        List<Order> orders = [];
        for (int index = 1; index <= 18; index++)
        {
            int customerNumber = (index - 1) % 6 + 1;
            CustomerDto customer = customers[customerNumber - 1];
            Order order = Order.Create($"SAMPLE-{index:D3}", customer);
            order.AddLine($"P-{index:D3}", $"Товар {index}", 50m * index, index % 3 + 1);
            if (index % 3 == 0)
                order.Confirm();
            else if (index % 3 == 1)
                order.Cancel();
            orders.Add(order);
        }
        return orders.AsReadOnly();
    }

    public static IReadOnlyList<Order> BenchmarkOrders(int count = 100_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var customers = Customers();
        return Enumerable.Range(1, count).Select(index =>
        {
            Order order = Order.Create($"BENCH-{index:D6}", customers[(index - 1) % customers.Count]);
            order.AddLine("BENCH-P", "Товар", index % 2000 + 1m, index % 3 + 1);
            if (index % 3 == 0) order.Confirm();
            else if (index % 3 == 1) order.Cancel();
            return order;
        }).ToList().AsReadOnly();
    }
}
