using PressingManager.Core.Common;
using PressingManager.Core.Enums;

namespace PressingManager.Tests.Unit;

public class OrderStatusRulesTests
{
    [Theory]
    [InlineData(OrderStatus.Received, OrderStatus.AwaitingProcessing)]
    [InlineData(OrderStatus.AwaitingProcessing, OrderStatus.Cleaning)]
    [InlineData(OrderStatus.Cleaning, OrderStatus.Ironing)]
    [InlineData(OrderStatus.Cleaning, OrderStatus.ReadyForPickup)]
    [InlineData(OrderStatus.Ironing, OrderStatus.ReadyForPickup)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Received, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.ReadyForPickup, OrderStatus.Cancelled)]
    public void CanTransition_NormalFlow_IsAllowed(OrderStatus from, OrderStatus to)
    {
        Assert.True(OrderStatusRules.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Received, OrderStatus.Delivered)]
    [InlineData(OrderStatus.AwaitingProcessing, OrderStatus.Ironing)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cleaning)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Received)]
    [InlineData(OrderStatus.Ironing, OrderStatus.Cleaning)]
    public void CanTransition_InvalidWithoutCorrection_IsRefused(OrderStatus from, OrderStatus to)
    {
        Assert.False(OrderStatusRules.CanTransition(from, to));
    }

    [Fact]
    public void CanTransition_DeliveredBackToCleaning_NeedsAuthorizedCorrection()
    {
        Assert.False(OrderStatusRules.CanTransition(OrderStatus.Delivered, OrderStatus.Cleaning));
        Assert.True(OrderStatusRules.CanTransition(OrderStatus.Delivered, OrderStatus.Cleaning, isAuthorizedCorrection: true));
    }

    [Fact]
    public void CanTransition_CancelledIsFinal_EvenWithCorrection()
    {
        Assert.False(OrderStatusRules.CanTransition(OrderStatus.Cancelled, OrderStatus.Received, isAuthorizedCorrection: true));
    }

    [Fact]
    public void CanTransition_CorrectionCannotMoveForwardOrToCancelled()
    {
        Assert.False(OrderStatusRules.CanTransition(OrderStatus.Received, OrderStatus.Delivered, isAuthorizedCorrection: true));
        Assert.False(OrderStatusRules.CanTransition(OrderStatus.Delivered, OrderStatus.Cancelled, isAuthorizedCorrection: true));
    }

    [Fact]
    public void CanTransition_SameStatus_IsRefused()
    {
        Assert.False(OrderStatusRules.CanTransition(OrderStatus.Cleaning, OrderStatus.Cleaning, isAuthorizedCorrection: true));
    }

    [Fact]
    public void GetNextStatuses_FinalStatuses_AreEmpty()
    {
        Assert.Empty(OrderStatusRules.GetNextStatuses(OrderStatus.Delivered));
        Assert.Empty(OrderStatusRules.GetNextStatuses(OrderStatus.Cancelled));
    }
}
