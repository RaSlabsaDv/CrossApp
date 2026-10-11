using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

public sealed class CachingOrderStore : IOrderStore
{
    private readonly IOrderStore _inner;
    private readonly Dictionary<string, OrderDto?> _byId = new(StringComparer.OrdinalIgnoreCase);
    private OrderDto[]? _list;

    public int CacheHits { get; private set; }
    public int CacheMisses { get; private set; }

    public CachingOrderStore(IOrderStore inner) =>
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public IReadOnlyList<Order> List()
    {
        if (_list is null)
        {
            CacheMisses++;
            _list = _inner.List().Select(order => order.ToDto()).ToArray();
        }
        else
            CacheHits++;
        return _list.Select(Order.FromDto).ToList().AsReadOnly();
    }

    public Order? GetById(string id)
    {
        string key = InMemoryOrderStore.ValidateId(id);
        if (!_byId.TryGetValue(key, out OrderDto? dto))
        {
            CacheMisses++;
            dto = _inner.GetById(key)?.ToDto();
            _byId[key] = dto;
        }
        else
            CacheHits++;
        return dto is null ? null : Order.FromDto(dto);
    }

    public void Add(Order item)
    {
        _inner.Add(item);
        Invalidate();
    }

    public void Update(Order item)
    {
        _inner.Update(item);
        Invalidate();
    }

    public bool Remove(string id)
    {
        bool removed = _inner.Remove(id);
        if (removed)
            Invalidate();
        return removed;
    }

    private void Invalidate()
    {
        _byId.Clear();
        _list = null;
    }
}
