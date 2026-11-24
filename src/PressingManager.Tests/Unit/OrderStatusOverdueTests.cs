using PressingManager.Core.Common;
using PressingManager.Core.Enums;

namespace PressingManager.Tests.Unit;

public class OrderStatusOverdueTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(OrderStatus.Received)]
    [InlineData(OrderStatus.AwaitingProcessing)]
    [InlineData(OrderStatus.Cleaning)]
    [InlineData(OrderStatus.Ironing)]
    public void IsOverdue_ExpectedDayPassed_AndNotReady_IsTrue(OrderStatus status)
    {
        Assert.True(OrderStatusRules.IsOverdue(status, new DateTime(2026, 11, 9), Now));
    }

    [Fact]
    public void IsOverdue_ExpectedToday_IsNotLateYet()
    {
        Assert.False(OrderStatusRules.IsOverdue(OrderStatus.Cleaning, new DateTime(2026, 11, 10), Now));
    }

    [Fact]
    public void IsOverdue_NoExpectedDate_IsFalse()
    {
        Assert.False(OrderStatusRules.IsOverdue(OrderStatus.Cleaning, null, Now));
    }

    [Theory]
    [InlineData(OrderStatus.ReadyForPickup)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void IsOverdue_ReadyOrClosedOrders_AreNeverOverdue(OrderStatus status)
    {
        Assert.False(OrderStatusRules.IsOverdue(status, new DateTime(2026, 11, 1), Now));
    }

    [Theory]
    [InlineData(OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.ReadyForPickup, false)]
    [InlineData(OrderStatus.Received, false)]
    public void IsClosed_OnlyDeliveredAndCancelled(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, OrderStatusRules.IsClosed(status));
    }
}
