using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

public sealed class FileOrderStore : IOrderStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;
    private InMemoryOrderStore _cache = new();
    private bool _loaded;

    public FileOrderStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Storage file path is required", nameof(path));
        _path = Path.GetFullPath(path);
    }

    public IReadOnlyList<Order> List()
    {
        EnsureLoaded();
        return _cache.List();
    }

    public Order? GetById(string id)
    {
        InMemoryOrderStore.ValidateId(id);
        EnsureLoaded();
        return _cache.GetById(id);
    }

    public void Add(Order item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();
        InMemoryOrderStore candidate = new(_cache.List());
        candidate.Add(item);
        Flush(candidate);
    }

    public void Update(Order item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();
        InMemoryOrderStore candidate = new(_cache.List());
        candidate.Update(item);
        Flush(candidate);
    }

    public bool Remove(string id)
    {
        InMemoryOrderStore.ValidateId(id);
        EnsureLoaded();
        InMemoryOrderStore candidate = new(_cache.List());
        if (!candidate.Remove(id))
            return false;
        Flush(candidate);
        return true;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;
        if (File.Exists(_path))
        {
            List<OrderDto> dtos = JsonSerializer.Deserialize<List<OrderDto>>(File.ReadAllText(_path))
                ?? throw new InvalidOperationException("Storage file must contain a JSON array of orders");
            // Publish the cache only after every DTO has passed domain checks.
            InMemoryOrderStore loaded = new(dtos.Select(Order.FromDto));
            _cache = loaded;
        }
        _loaded = true;
    }

    private void Flush(InMemoryOrderStore candidate)
    {
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            string json = JsonSerializer.Serialize(candidate.List().Select(order => order.ToDto()), Options);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
            _cache = candidate;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
