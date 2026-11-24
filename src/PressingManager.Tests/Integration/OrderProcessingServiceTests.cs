using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

public class OrderProcessingServiceTests : IDisposable
{
    private readonly ProcessingTestDb _db = new();
    private readonly ProcessingTestClock _clock = new(new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly int _userId;

    public OrderProcessingServiceTests()
    {
        _userId = _db.AddUser();
    }

    public void Dispose() => _db.Dispose();

    private OrderProcessingService CreateService() => new(_db, _clock);

    private async Task Move(OrderProcessingService service, int orderId, params OrderStatus[] statuses)
    {
        foreach (var status in statuses)
        {
            _clock.Advance(TimeSpan.FromMinutes(10));
            await service.ChangeStatusAsync(new ChangeOrderStatusDto(orderId, status, _userId));
        }
    }

    private OrderStatus StatusOf(int orderId)
    {
        using var db = _db.CreateDbContext();
        return db.Orders.AsNoTracking().Single(o => o.Id == orderId).Status;
    }

    [Fact]
    public async Task ChangeStatusAsync_FirstChange_StartsFromTheOrderStatus_AndUpdatesTheOrder()
    {
        var service = CreateService();
        var orderId = _db.AddOrder();

        var entry = await service.ChangeStatusAsync(
            new ChangeOrderStatusDto(orderId, OrderStatus.AwaitingProcessing, _userId));

        Assert.Equal(OrderStatus.Received, entry.FromStatus);
        Assert.Equal(OrderStatus.AwaitingProcessing, entry.ToStatus);
        Assert.False(entry.IsCorrection);
        Assert.Equal(_userId, entry.ChangedByUserId);
        Assert.Equal(OrderStatus.AwaitingProcessing, StatusOf(orderId));
    }

    [Fact]
    public async Task ChangeStatusAsync_UsesTheCurrentStatusOfTheOrder()
    {
        var service = CreateService();
        var orderId = _db.AddOrder(OrderStatus.Cleaning);

        var entry = await service.ChangeStatusAsync(new ChangeOrderStatusDto(orderId, OrderStatus.Ironing, _userId));

        Assert.Equal(OrderStatus.Cleaning, entry.FromStatus);
        Assert.Equal(OrderStatus.Ironing, StatusOf(orderId));
    }

    [Fact]
    public async Task ChangeStatusAsync_FullFlow_IsRecordedInOrder()
    {
        var service = CreateService();
        var orderId = _db.AddOrder();

        await Move(service, orderId,
            OrderStatus.AwaitingProcessing,
            OrderStatus.Cleaning,
            OrderStatus.Ironing,
            OrderStatus.ReadyForPickup,
            OrderStatus.Delivered);

        var history = await service.GetStatusHistoryAsync(orderId);

        Assert.Equal(
            new[]
            {
                OrderStatus.AwaitingProcessing, OrderStatus.Cleaning, OrderStatus.Ironing,
                OrderStatus.ReadyForPickup, OrderStatus.Delivered
            },
            history.Select(h => h.ToStatus).ToArray());
        Assert.All(history, h => Assert.False(h.IsCorrection));
        Assert.Equal(OrderStatus.Delivered, StatusOf(orderId));
    }

    [Fact]
    public async Task ChangeStatusAsync_InvalidTransition_IsRefusedAndNothingIsWritten()
    {
        var service = CreateService();
        var orderId = _db.AddOrder();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ChangeStatusAsync(new ChangeOrderStatusDto(orderId, OrderStatus.Delivered, _userId)));

        Assert.Empty(await service.GetStatusHistoryAsync(orderId));
        Assert.Equal(OrderStatus.Received, StatusOf(orderId));
    }

    [Fact]
    public async Task ChangeStatusAsync_BackwardWithoutAuthorization_IsRefused()
    {
        var service = CreateService();
        var orderId = _db.AddOrder();
        await Move(service, orderId, OrderStatus.AwaitingProcessing, OrderStatus.Cleaning, OrderStatus.ReadyForPickup, OrderStatus.Delivered);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ChangeStatusAsync(new ChangeOrderStatusDto(orderId, OrderStatus.Cleaning, _userId)));

        Assert.Equal(OrderStatus.Delivered, StatusOf(orderId));
    }

    [Fact]
    public async Task ChangeStatusAsync_AuthorizedCorrection_IsAcceptedAndFlagged()
    {
        var service = CreateService();
        var orderId = _db.AddOrder();
        await Move(service, orderId, OrderStatus.AwaitingProcessing, OrderStatus.Cleaning, OrderStatus.ReadyForPickup, OrderStatus.Delivered);

        var entry = await service.ChangeStatusAsync(new ChangeOrderStatusDto(
            orderId, OrderStatus.ReadyForPickup, _userId, IsAuthorizedCorrection: true, Comment: "  Livré par erreur  "));

        Assert.True(entry.IsCorrection);
        Assert.Equal(OrderStatus.Delivered, entry.FromStatus);
        Assert.Equal("Livré par erreur", entry.Comment);
        Assert.Equal(OrderStatus.ReadyForPickup, StatusOf(orderId));
    }

    [Fact]
    public async Task ChangeStatusAsync_NormalTransitionWithCorrectionFlag_IsNotACorrection()
    {
        var service = CreateService();
        var orderId = _db.AddOrder();

        var entry = await service.ChangeStatusAsync(new ChangeOrderStatusDto(
            orderId, OrderStatus.AwaitingProcessing, _userId, IsAuthorizedCorrection: true));

        Assert.False(entry.IsCorrection);
    }

    [Fact]
    public async Task ChangeStatusAsync_CancelledOrderStaysCancelled()
    {
        var service = CreateService();
        var orderId = _db.AddOrder();
        await Move(service, orderId, OrderStatus.Cancelled);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ChangeStatusAsync(new ChangeOrderStatusDto(
                orderId, OrderStatus.Received, _userId, IsAuthorizedCorrection: true)));
    }

    [Fact]
    public async Task ChangeStatusAsync_UnknownOrder_IsRefused()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ChangeStatusAsync(new ChangeOrderStatusDto(999, OrderStatus.AwaitingProcessing, _userId)));
    }

    [Fact]
    public async Task ChangeStatusAsync_UnknownOrInactiveUser_IsRefused()
    {
        var service = CreateService();
        var orderId = _db.AddOrder();
        var inactiveUserId = _db.AddUser(isActive: false);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ChangeStatusAsync(new ChangeOrderStatusDto(orderId, OrderStatus.AwaitingProcessing, 9999)));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ChangeStatusAsync(new ChangeOrderStatusDto(orderId, OrderStatus.AwaitingProcessing, inactiveUserId)));

        Assert.Equal(OrderStatus.Received, StatusOf(orderId));
    }

    [Fact]
    public async Task GetStatusHistoryAsync_IsolatesOrders()
    {
        var service = CreateService();
        var first = _db.AddOrder();
        var second = _db.AddOrder();

        await Move(service, first, OrderStatus.AwaitingProcessing);
        await Move(service, second, OrderStatus.Cancelled);

        Assert.Single(await service.GetStatusHistoryAsync(first));
        Assert.Single(await service.GetStatusHistoryAsync(second));
    }

    [Fact]
    public void GetAllowedNextStatuses_FollowsTheBusinessRules()
    {
        var service = CreateService();

        Assert.Contains(OrderStatus.Cleaning, service.GetAllowedNextStatuses(OrderStatus.AwaitingProcessing));
        Assert.Empty(service.GetAllowedNextStatuses(OrderStatus.Delivered));
    }
}
