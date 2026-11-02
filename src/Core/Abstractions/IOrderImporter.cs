using Core.Domain;

namespace Core.Abstractions;

public interface IOrderImporter
{
    Task<IReadOnlyList<Order>> LoadAsync(string path, CancellationToken ct = default);
    IAsyncEnumerable<Order> StreamAsync(string path, CancellationToken ct = default);
}
