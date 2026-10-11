using Core.Domain;
using Core.Dto;

namespace Core;

public static class SampleData
{
    public static IReadOnlyList<Order> Orders()
    {
        List<Order> orders = [];
        for (int index = 1; index <= 18; index++)
        {
            int customerNumber = (index - 1) % 6 + 1;
            CustomerDto customer = new($"C-{customerNumber:D3}",
                $"Клієнт {customerNumber}", $"customer{customerNumber}@example.com");
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
}
