using Core.Domain;
using Core.Events;
using Core.Services;
using Core.Storage;

namespace Core.Tests;

public sealed class OrderServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddLine_InvalidQuantity_ThrowsAndKeepsState(int quantity)
    {
        var store = new InMemoryOrderStore([TestData.Order()]);
        var service = new OrderService(store);
        var events = new List<OrderChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.AddLine("ORD-A", "P-2", "Миша", 5m, quantity));
        Assert.Equal(20m, store.GetById("ORD-A")!.Total);
        Assert.Single(store.GetById("ORD-A")!.Lines);
        Assert.Empty(events);
    }

    [Fact]
    public void CreateOrder_RaisesChangedExactlyOnceAfterSaving()
    {
        var store = new InMemoryOrderStore();
        var service = new OrderService(store);
        var events = new List<OrderChangedEventArgs>();
        service.Changed += (_, e) => { Assert.NotNull(store.GetById(e.OrderId)); events.Add(e); };
        service.CreateOrder(TestData.Customer, "ORD-A");
        var change = Assert.Single(events);
        Assert.Equal(OrderChangeKind.OrderCreated, change.Kind);
        Assert.Equal("ORD-A", change.OrderId);
    }

    [Fact]
    public void AddLines_ComputesOrderTotal()
    {
        var service = new OrderService(new InMemoryOrderStore());
        service.CreateOrder(TestData.Customer, "ORD-A");
        service.AddLine("ORD-A", "P-1", "Кабель", 12.5m, 2);
        service.AddLine("ORD-A", "P-2", "Миша", 30m, 3);
        Assert.Equal(115m, service.Find("ORD-A")!.Total);
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_ThrowsDomainExceptionWithoutEvent()
    {
        var service = new OrderService(new InMemoryOrderStore([TestData.Order()]));
        service.Confirm("ORD-A");
        int changes = 0;
        service.Changed += (_, _) => changes++;
        Assert.Throws<DomainException>(() => service.Confirm("ORD-A"));
        Assert.Equal(OrderStatus.Confirmed, service.Find("ORD-A")!.Status);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Confirm_EmptyOrder_KeepsDraft()
    {
        var service = new OrderService(new InMemoryOrderStore());
        service.CreateOrder(TestData.Customer, "ORD-A");
        Assert.Throws<DomainException>(() => service.Confirm("ORD-A"));
        Assert.Equal(OrderStatus.Draft, service.Find("ORD-A")!.Status);
    }

    [Fact]
    public void Confirm_SixthOrder_RejectsUntilAnotherIsCancelled()
    {
        var service = new OrderService(new InMemoryOrderStore());
        for (int i = 0; i < 6; i++)
        {
            service.CreateOrder(TestData.Customer, $"ORD-{i}");
            service.AddLine($"ORD-{i}", "P", "Кабель", 1m, 1);
            if (i < 5) service.Confirm($"ORD-{i}");
        }
        Assert.Throws<DomainException>(() => service.Confirm("ORD-5"));
        service.Cancel("ORD-0");
        service.Confirm("ORD-5");
        Assert.True(service.Find("ORD-5")!.IsConfirmed);
    }
}
