using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

public class OrderProcessingQueueTests : IDisposable
{
    // Horloge fixee au 10 novembre 2026.
    private readonly ProcessingTestDb _db = new();
    private readonly ProcessingTestClock _clock = new(new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero));
    private readonly int _userId;

    public OrderProcessingQueueTests()
    {
        _userId = _db.AddUser();
    }

    public void Dispose() => _db.Dispose();

    private OrderProcessingService CreateService(UserRole role = UserRole.Administrator)
    {
        var session = ProcessingTestSession.SignedIn(role, _userId);
        return new(_db, session, new AuditService(_db, session, _clock), _clock);
    }

    [Fact]
    public async Task GetOrdersAsync_WithoutStatus_ReturnsOnlyOrdersStillToHandle()
    {
        var received = _db.AddOrder(OrderStatus.Received);
        var cleaning = _db.AddOrder(OrderStatus.Cleaning);
        var ready = _db.AddOrder(OrderStatus.ReadyForPickup);
        _db.AddOrder(OrderStatus.Delivered);
        _db.AddOrder(OrderStatus.Cancelled);

        var orders = await CreateService().GetOrdersAsync();

        Assert.Equal(
            new[] { received, cleaning, ready }.OrderBy(id => id),
            orders.Select(o => o.OrderId).OrderBy(id => id));
    }

    [Fact]
    public async Task GetOrdersAsync_WithStatus_ReturnsOnlyThatStatus()
    {
        _db.AddOrder(OrderStatus.Received);
        var ready = _db.AddOrder(OrderStatus.ReadyForPickup);

        var orders = await CreateService().GetOrdersAsync(OrderStatus.ReadyForPickup);

        var only = Assert.Single(orders);
        Assert.Equal(ready, only.OrderId);
        Assert.True(only.IsReadyForPickup);
    }

    [Fact]
    public async Task GetOrdersAsync_DeliveredStatus_CanBeListedExplicitly()
    {
        var delivered = _db.AddOrder(OrderStatus.Delivered);

        var orders = await CreateService().GetOrdersAsync(OrderStatus.Delivered);

        Assert.Equal(delivered, Assert.Single(orders).OrderId);
    }

    [Fact]
    public async Task GetOrdersAsync_SortsMostUrgentFirst_AndFillsDetails()
    {
        var later = _db.AddOrder(OrderStatus.Cleaning, new DateTime(2026, 11, 20), itemQuantity: 3);
        var sooner = _db.AddOrder(OrderStatus.Cleaning, new DateTime(2026, 11, 12));
        var noDate = _db.AddOrder(OrderStatus.Cleaning);

        var orders = await CreateService().GetOrdersAsync();

        Assert.Equal(new[] { sooner, later, noDate }, orders.Select(o => o.OrderId));
        Assert.Equal("Client test", orders[0].CustomerName);
        Assert.Equal(3, orders.Single(o => o.OrderId == later).ItemCount);
    }

    [Fact]
    public async Task GetOverdueOrdersAsync_ReturnsLateOrdersNotYetReady()
    {
        var late = _db.AddOrder(OrderStatus.Cleaning, new DateTime(2026, 11, 8));
        _db.AddOrder(OrderStatus.Cleaning, new DateTime(2026, 11, 10)); // prevue aujourd'hui
        _db.AddOrder(OrderStatus.ReadyForPickup, new DateTime(2026, 11, 8)); // prete : pas un retard
        _db.AddOrder(OrderStatus.Delivered, new DateTime(2026, 11, 1));

        var overdue = await CreateService().GetOverdueOrdersAsync();

        var only = Assert.Single(overdue);
        Assert.Equal(late, only.OrderId);
        Assert.True(only.IsOverdue);
    }

    [Fact]
    public async Task Queries_WithoutViewOrdersPermission_AreDenied()
    {
        // Le responsable de stock n'a pas ViewOrders.
        var service = CreateService(UserRole.StockManager);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetOrdersAsync());
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetOverdueOrdersAsync());
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetStatusHistoryAsync(1));
    }

    [Fact]
    public async Task Processor_CanViewTheQueue()
    {
        _db.AddOrder(OrderStatus.Received);

        var orders = await CreateService(UserRole.Processor).GetOrdersAsync();

        Assert.Single(orders);
    }
}
