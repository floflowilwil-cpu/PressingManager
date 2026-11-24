using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

public class OrderProcessingServiceTests : IDisposable
{
    private const int OrderId = 42;
    private const int UserId = 7;

    private readonly ProcessingTestDb _db = new();
    private readonly ProcessingTestClock _clock = new(new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero));

    public void Dispose() => _db.Dispose();

    private OrderProcessingService CreateService() => new(_db.CreateContext(), _clock);

    private async Task Move(OrderProcessingService service, params OrderStatus[] statuses)
    {
        foreach (var status in statuses)
        {
            _clock.Advance(TimeSpan.FromMinutes(10));
            await service.ChangeStatusAsync(new ChangeOrderStatusDto(OrderId, status, UserId));
        }
    }

    [Fact]
    public async Task ChangeStatusAsync_FirstChange_StartsFromReceived()
    {
        var service = CreateService();

        var entry = await service.ChangeStatusAsync(
            new ChangeOrderStatusDto(OrderId, OrderStatus.AwaitingProcessing, UserId));

        Assert.Equal(OrderStatus.Received, entry.FromStatus);
        Assert.Equal(OrderStatus.AwaitingProcessing, entry.ToStatus);
        Assert.False(entry.IsCorrection);
        Assert.Equal(UserId, entry.ChangedByUserId);
    }

    [Fact]
    public async Task ChangeStatusAsync_FullFlow_IsRecordedInOrder()
    {
        var service = CreateService();

        await Move(service,
            OrderStatus.AwaitingProcessing,
            OrderStatus.Cleaning,
            OrderStatus.Ironing,
            OrderStatus.ReadyForPickup,
            OrderStatus.Delivered);

        var history = await service.GetStatusHistoryAsync(OrderId);

        Assert.Equal(
            new[]
            {
                OrderStatus.AwaitingProcessing, OrderStatus.Cleaning, OrderStatus.Ironing,
                OrderStatus.ReadyForPickup, OrderStatus.Delivered
            },
            history.Select(h => h.ToStatus).ToArray());
        Assert.All(history, h => Assert.False(h.IsCorrection));
    }

    [Fact]
    public async Task ChangeStatusAsync_InvalidTransition_IsRefusedAndNotRecorded()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeStatusAsync(new ChangeOrderStatusDto(OrderId, OrderStatus.Delivered, UserId)));

        Assert.Empty(await service.GetStatusHistoryAsync(OrderId));
    }

    [Fact]
    public async Task ChangeStatusAsync_BackwardWithoutAuthorization_IsRefused()
    {
        var service = CreateService();
        await Move(service, OrderStatus.AwaitingProcessing, OrderStatus.Cleaning, OrderStatus.ReadyForPickup, OrderStatus.Delivered);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeStatusAsync(new ChangeOrderStatusDto(OrderId, OrderStatus.Cleaning, UserId)));
    }

    [Fact]
    public async Task ChangeStatusAsync_AuthorizedCorrection_IsAcceptedAndFlagged()
    {
        var service = CreateService();
        await Move(service, OrderStatus.AwaitingProcessing, OrderStatus.Cleaning, OrderStatus.ReadyForPickup, OrderStatus.Delivered);

        var entry = await service.ChangeStatusAsync(new ChangeOrderStatusDto(
            OrderId, OrderStatus.ReadyForPickup, UserId, IsAuthorizedCorrection: true, Comment: "  Livré par erreur  "));

        Assert.True(entry.IsCorrection);
        Assert.Equal(OrderStatus.Delivered, entry.FromStatus);
        Assert.Equal("Livré par erreur", entry.Comment);
    }

    [Fact]
    public async Task ChangeStatusAsync_NormalTransitionWithCorrectionFlag_IsNotACorrection()
    {
        var service = CreateService();

        var entry = await service.ChangeStatusAsync(new ChangeOrderStatusDto(
            OrderId, OrderStatus.AwaitingProcessing, UserId, IsAuthorizedCorrection: true));

        Assert.False(entry.IsCorrection);
    }

    [Fact]
    public async Task ChangeStatusAsync_CancelledOrderStaysCancelled()
    {
        var service = CreateService();
        await Move(service, OrderStatus.Cancelled);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeStatusAsync(new ChangeOrderStatusDto(
                OrderId, OrderStatus.Received, UserId, IsAuthorizedCorrection: true)));
    }

    [Fact]
    public async Task GetStatusHistoryAsync_IsolatesOrders()
    {
        var service = CreateService();
        await Move(service, OrderStatus.AwaitingProcessing);
        await service.ChangeStatusAsync(new ChangeOrderStatusDto(99, OrderStatus.Cancelled, UserId));

        Assert.Single(await service.GetStatusHistoryAsync(OrderId));
        Assert.Single(await service.GetStatusHistoryAsync(99));
    }

    [Fact]
    public void GetAllowedNextStatuses_FollowsTheBusinessRules()
    {
        var service = CreateService();

        Assert.Contains(OrderStatus.Cleaning, service.GetAllowedNextStatuses(OrderStatus.AwaitingProcessing));
        Assert.Empty(service.GetAllowedNextStatuses(OrderStatus.Delivered));
    }
}
