using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

public sealed class InMemoryOrderStore : IOrderStore
{
    private readonly Dictionary<string, OrderDto> _items = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryOrderStore(IEnumerable<Order>? seed = null)
    {
        foreach (Order order in seed ?? [])
            Add(order);
    }

    // Return detached entities: changes are persisted only through Update.
    public IReadOnlyList<Order> List() =>
        _items.Values.Select(Order.FromDto).ToList().AsReadOnly();

    public Order? GetById(string id)
    {
        string key = ValidateId(id);
        return _items.TryGetValue(key, out OrderDto? dto) ? Order.FromDto(dto) : null;
    }

    public void Add(Order item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_items.ContainsKey(item.Id))
            throw new InvalidOperationException($"Order with ID {item.Id} already exists");
        _items.Add(item.Id, item.ToDto());
    }

    public void Update(Order item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_items.ContainsKey(item.Id))
            throw new InvalidOperationException($"Order with ID {item.Id} does not exist");
        _items[item.Id] = item.ToDto();
    }

    public bool Remove(string id) => _items.Remove(ValidateId(id));

    internal static string ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Order ID is required", nameof(id));
        return id.Trim();
    }
}
