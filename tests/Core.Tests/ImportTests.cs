using System.Runtime.CompilerServices;
using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Events;
using Core.Services;
using Core.Storage;

namespace Core.Tests;

public sealed class ImportTests
{
    private sealed class FakeImporter(IReadOnlyList<Order> items) : IOrderImporter
    {
        public CancellationToken ReceivedToken { get; private set; }
        public Task<IReadOnlyList<Order>> LoadAsync(string path, CancellationToken ct = default)
        {
            ReceivedToken = ct;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(items);
        }
        public async IAsyncEnumerable<Order> StreamAsync(string path,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ReceivedToken = ct;
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                await Task.CompletedTask;
                yield return item;
            }
        }
    }

    private sealed class CallbackProgress(Action<int> callback) : IProgress<int>
    {
        public void Report(int value) => callback(value);
    }

    private sealed class WaitingImporter : IOrderImporter
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<Order>> LoadAsync(string path, CancellationToken ct = default)
        {
            Entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return [];
        }
        public IAsyncEnumerable<Order> StreamAsync(string path, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_IsIdempotentAndRaisesOneEventPerAddedOrder(bool streaming)
    {
        var service = new OrderService(new InMemoryOrderStore());
        var importer = new FakeImporter([TestData.Order("ORD-A"), TestData.Order("ord-a"), TestData.Order("ORD-B")]);
        var changes = new List<OrderChangedEventArgs>();
        service.Changed += (_, e) => changes.Add(e);
        using var cts = new CancellationTokenSource();
        Task<int> Import() => streaming ? service.ImportStreamAsync(importer, "unused", cts.Token)
            : service.ImportAsync(importer, "unused", cts.Token);
        Assert.Equal(2, await Import());
        Assert.Equal(0, await Import());
        Assert.Equal(cts.Token, importer.ReceivedToken);
        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.Equal(OrderChangeKind.OrderCreated, change.Kind));
        Assert.Equal(2, service.All().Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_AlreadyCancelled_DoesNotOpenMissingFile(bool streaming)
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var service = new OrderService(new InMemoryOrderStore());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => streaming
            ? service.ImportStreamAsync(new JsonOrderImporter(), "any.json", cts.Token)
            : service.ImportAsync(new JsonOrderImporter(), "any.json", cts.Token));
        Assert.Empty(service.All());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_CancelAfterFirstEvent_KeepsOnlyCompletedWrites(bool streaming)
    {
        using var cts = new CancellationTokenSource();
        var service = new OrderService(new InMemoryOrderStore());
        int events = 0;
        service.Changed += (_, _) => { events++; cts.Cancel(); };
        var importer = new FakeImporter(TestData.Items());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => streaming
            ? service.ImportStreamAsync(importer, "unused", cts.Token)
            : service.ImportAsync(importer, "unused", cts.Token));
        Assert.Single(service.All());
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task LinkedToken_TimeoutCancelsPendingImportWhileUserTokenRemainsActive()
    {
        using var user = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(user.Token, timeout.Token);
        var importer = new WaitingImporter();
        var service = new OrderService(new InMemoryOrderStore());
        var operation = service.ImportAsync(importer, "unused", linked.Token);
        await importer.Entered.Task;
        timeout.CancelAfter(TimeSpan.FromMilliseconds(30));
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.True(timeout.IsCancellationRequested);
        Assert.False(user.IsCancellationRequested);
        Assert.Equal(linked.Token, error.CancellationToken);
        Assert.Empty(service.All());
    }

    [Fact]
    public async Task Import_ReportsOrderedPercentagesIncludingDuplicates()
    {
        var service = new OrderService(new InMemoryOrderStore([TestData.Order("ORD-B")]));
        var values = new List<int>();
        Assert.Equal(2, await service.ImportAsync(new FakeImporter(TestData.Items()), "unused",
            progress: new CallbackProgress(values.Add)));
        Assert.Equal(new[] { 0, 33, 66, 100 }, values);
    }

    [Fact]
    public async Task Import_EmptyInput_ReportsCompletion()
    {
        var values = new List<int>();
        var service = new OrderService(new InMemoryOrderStore());
        Assert.Equal(0, await service.ImportAsync(new FakeImporter([]), "unused",
            progress: new CallbackProgress(values.Add)));
        Assert.Equal(new[] { 0, 100 }, values);
    }

    [Fact]
    public async Task Import_CannotBypassConfirmedOrderLimit()
    {
        var orders = Enumerable.Range(0, 6).Select(i => TestData.Order($"ORD-{i}")).ToArray();
        foreach (var order in orders) order.Confirm();
        var service = new OrderService(new InMemoryOrderStore(orders.Take(5)));
        await Assert.ThrowsAsync<DomainException>(() => service.ImportAsync(new FakeImporter([orders[5]]), "unused"));
        Assert.Equal(5, service.All().Count);
    }

    // The only test using a temporary file. Each scenario overwrites the same isolated path.
    [Fact]
    public async Task JsonImporter_ValidatesRecordsAndPreservesTechnicalErrors()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lab08-{Guid.NewGuid():N}.json");
        var importer = new JsonOrderImporter();
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(TestData.Items().Select(x => x.ToDto())));
            Assert.Equal(3, (await importer.LoadAsync(path)).Count);
            var streamed = new List<Order>();
            await foreach (var order in importer.StreamAsync(path)) streamed.Add(order);
            Assert.Equal(new[] { "ORD-B", "ORD-A", "ORD-C" }, streamed.Select(x => x.Id));

            var valid = TestData.Order().ToDto();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new[] { valid, valid with { TotalPrice = -1m } }));
            var error = await Assert.ThrowsAsync<DomainException>(() => importer.LoadAsync(path));
            Assert.Contains("Запис 2:", error.Message);
            Assert.IsType<ArgumentException>(error.InnerException);
            var service = new OrderService(new InMemoryOrderStore());
            await Assert.ThrowsAsync<DomainException>(() => service.ImportAsync(importer, path));
            Assert.Empty(service.All());
            await Assert.ThrowsAsync<DomainException>(async () =>
            {
                await foreach (var _ in importer.StreamAsync(path)) { }
            });

            await File.WriteAllTextAsync(path, "[null]");
            Assert.Contains("Запис 1:", (await Assert.ThrowsAsync<DomainException>(() => importer.LoadAsync(path))).Message);
            await File.WriteAllTextAsync(path, "null");
            await Assert.ThrowsAsync<DomainException>(() => importer.LoadAsync(path));
            await File.WriteAllTextAsync(path, "{broken json");
            await Assert.ThrowsAsync<JsonException>(() => importer.LoadAsync(path));
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => importer.LoadAsync(path, cts.Token));
        }
        finally
        {
            File.Delete(path);
        }
        await Assert.ThrowsAsync<FileNotFoundException>(() => importer.LoadAsync(path));
    }
}
