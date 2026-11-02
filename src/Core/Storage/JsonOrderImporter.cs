using System.Runtime.CompilerServices;
using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

public sealed class JsonOrderImporter : IOrderImporter
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<Order>> LoadAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = Open(path);
        var dtos = await JsonSerializer.DeserializeAsync<List<OrderDto?>>(stream, Options, ct)
            ?? throw new DomainException($"Файл {Path.GetFileName(path)} не містить списку.");
        var result = new List<Order>(dtos.Count);
        for (int i = 0; i < dtos.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            result.Add(Convert(dtos[i], i + 1, ct));
        }
        return result.AsReadOnly();
    }

    public async IAsyncEnumerable<Order> StreamAsync(string path,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = Open(path);
        int index = 0;
        await foreach (var dto in JsonSerializer.DeserializeAsyncEnumerable<OrderDto>(
            stream, Options, ct).WithCancellation(ct))
        {
            ct.ThrowIfCancellationRequested();
            yield return Convert(dto, ++index, ct);
        }
    }

    private static FileStream Open(string path) => new(path, FileMode.Open,
        FileAccess.Read, FileShare.Read, 4096, useAsync: true);

    private static Order Convert(OrderDto? dto, int index, CancellationToken ct)
    {
        try
        {
            return Order.FromDto(dto!, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or DomainException)
        {
            throw new DomainException($"Запис {index}: {ex.Message}", ex);
        }
    }
}
