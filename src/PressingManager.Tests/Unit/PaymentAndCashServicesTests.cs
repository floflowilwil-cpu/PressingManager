using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Repositories;
using PressingManager.Infrastructure.Services;

namespace PressingManager.Tests.Unit;

public class PaymentAndCashServicesTests
{
    private sealed class FakeOrderBillingInfoProvider : IOrderBillingInfoProvider
    {
        private readonly Dictionary<int, OrderBillingInfoDto> _orders = new();

        public void SetOrder(int orderId, decimal totalAmount, bool isCancelled = false)
        {
            _orders[orderId] = new OrderBillingInfoDto(orderId, totalAmount, isCancelled);
        }

        public Task<OrderBillingInfoDto?> GetOrderBillingInfoAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            _orders.TryGetValue(orderId, out var order);
            return Task.FromResult(order);
        }
    }

    private static (
        PaymentService PaymentService,
        ExpenseService ExpenseService,
        CashService CashService,
        FakeOrderBillingInfoProvider OrderProvider) CreateSut()
    {
        var paymentRepo = new InMemoryPaymentRepository();
        var expenseRepo = new InMemoryExpenseRepository();
        var cashRepo = new InMemoryCashSessionRepository();
        var orderProvider = new FakeOrderBillingInfoProvider();

        return (
            new PaymentService(paymentRepo, cashRepo, orderProvider),
            new ExpenseService(expenseRepo, cashRepo),
            new CashService(cashRepo, paymentRepo, expenseRepo),
            orderProvider);
    }

    [Fact]
    public async Task RegisterAndBalance_UpdatesRemainingBalanceAcrossPartialPayments()
    {
        var (paymentService, _, cashService, orders) = CreateSut();
        orders.SetOrder(10, 10000m);

        var session = await cashService.OpenAsync(new OpenCashSessionDto(5000m, 1));
        var first = await paymentService.RegisterAsync(
            new RegisterPaymentDto(10, 4000m, PaymentMethod.Cash, 1));
        var second = await paymentService.RegisterAsync(
            new RegisterPaymentDto(10, 3500m, PaymentMethod.MtnMobileMoney, 1, "MTN-8899"));

        var balance = await paymentService.GetBalanceAsync(10);
        var history = await paymentService.GetByOrderAsync(10);

        Assert.Equal(session.Id, first.CashSessionId);
        Assert.Null(second.CashSessionId);
        Assert.Equal(2500m, balance);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task RegisterAsync_AllowsSameMobileMoneyReferenceAcrossDifferentOrders()
    {
        var (paymentService, _, _, orders) = CreateSut();
        orders.SetOrder(1, 5000m);
        orders.SetOrder(2, 5000m);

        await paymentService.RegisterAsync(
            new RegisterPaymentDto(1, 2000m, PaymentMethod.OrangeMoney, 1, "OM-777"));

        await paymentService.RegisterAsync(
            new RegisterPaymentDto(2, 3000m, PaymentMethod.OrangeMoney, 1, " om-777 "));

        Assert.Equal(2000m, await paymentService.GetBalanceAsync(2));
    }

    [Fact]
    public async Task RegisterAsync_RefusesOverpaymentAndCancelledOrderAndClosedCash()
    {
        var (paymentService, _, cashService, orders) = CreateSut();
        orders.SetOrder(1, 3000m);
        orders.SetOrder(2, 3000m, isCancelled: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            paymentService.RegisterAsync(new RegisterPaymentDto(1, 1000m, PaymentMethod.Cash, 1)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            paymentService.RegisterAsync(new RegisterPaymentDto(2, 1000m, PaymentMethod.Other, 1)));

        await cashService.OpenAsync(new OpenCashSessionDto(0m, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            paymentService.RegisterAsync(new RegisterPaymentDto(1, 3500m, PaymentMethod.Cash, 1)));
    }

    [Fact]
    public async Task CancelAndRefundAsync_RestoreBalanceAndRequireReason()
    {
        var (paymentService, _, cashService, orders) = CreateSut();
        orders.SetOrder(1, 5000m);
        await cashService.OpenAsync(new OpenCashSessionDto(1000m, 1));

        var p1 = await paymentService.RegisterAsync(new RegisterPaymentDto(1, 2000m, PaymentMethod.Cash, 1));
        var p2 = await paymentService.RegisterAsync(new RegisterPaymentDto(1, 3000m, PaymentMethod.MtnMobileMoney, 1, "TX-55"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            paymentService.CancelAsync(new CancelPaymentDto(p1.Id, "   ", 2)));

        await paymentService.CancelAsync(new CancelPaymentDto(p1.Id, "Erreur montant", 2));
        await paymentService.RefundAsync(new CancelPaymentDto(p2.Id, "Client rembourse", 2));

        var balance = await paymentService.GetBalanceAsync(1);
        var payments = await paymentService.GetByOrderAsync(1);

        Assert.Equal(5000m, balance);
        Assert.Equal(PaymentStatus.Cancelled, payments[0].Status);
        Assert.Equal(PaymentStatus.Refunded, payments[1].Status);
        Assert.Equal("Erreur montant", payments[0].CancelReason);
    }

    [Fact]
    public async Task CashSessionAndExpenses_ComputeExpectedAmountAndDiscrepancyAtClose()
    {
        var (paymentService, expenseService, cashService, orders) = CreateSut();
        orders.SetOrder(1, 20000m);

        var session = await cashService.OpenAsync(new OpenCashSessionDto(10000m, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cashService.OpenAsync(new OpenCashSessionDto(5000m, 1)));

        await paymentService.RegisterAsync(new RegisterPaymentDto(1, 8000m, PaymentMethod.Cash, 1));
        await paymentService.RegisterAsync(new RegisterPaymentDto(1, 5000m, PaymentMethod.OrangeMoney, 1, "OM-1"));

        var e1 = await expenseService.CreateAsync(
            new CreateExpenseDto(DateTime.UtcNow, "Detergent", 3000m, PaymentMethod.Cash, 1, "Bidon 5L"));
        var e2 = await expenseService.CreateAsync(
            new CreateExpenseDto(DateTime.UtcNow, "Erreur", 1000m, PaymentMethod.Cash, 1));
        await expenseService.CancelAsync(e2.Id, "Doublon", 1);

        // Expected cash: 10000 + 8000 - 3000 = 15000 (OM et depense annulee ignores)
        var closed = await cashService.CloseAsync(
            new CloseCashSessionDto(session.Id, 14500m, 1, "Manquant 500"));

        Assert.Equal(CashSessionStatus.Closed, closed.Status);
        Assert.Equal(15000m, closed.ExpectedAmount);
        Assert.Equal(-500m, closed.Discrepancy);
        Assert.Null(await cashService.GetCurrentAsync());

        var search = await expenseService.SearchAsync(
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(1),
            "detergent");
        Assert.Single(search);
        Assert.Equal(e1.Id, search[0].Id);
    }

    [Fact]
    public async Task ExpenseService_RefusesCashExpenseWhenNoCashSessionIsOpen()
    {
        var (_, expenseService, _, _) = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            expenseService.CreateAsync(
                new CreateExpenseDto(DateTime.UtcNow, "Fournitures", 1500m, PaymentMethod.Cash, 1)));
    }
}
