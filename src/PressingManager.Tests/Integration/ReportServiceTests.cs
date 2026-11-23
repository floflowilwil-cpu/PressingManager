using PressingManager.Core.Common;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using static PressingManager.Tests.Integration.ReportTestContext;

namespace PressingManager.Tests.Integration;

public sealed class ReportServiceTests : IDisposable
{
    private readonly ReportTestContext _ctx = new();
    private readonly ReportService _reports;

    public ReportServiceTests()
    {
        _reports = new ReportService(_ctx.Db, _ctx.Session);
    }

    public void Dispose() => _ctx.Dispose();

    [Theory]
    [InlineData(UserRole.FrontDesk)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Processor)]
    [InlineData(UserRole.StockManager)]
    public async Task EveryReport_RequiresTheViewReportsPermission(UserRole role)
    {
        _ctx.SignInAs(role);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _reports.GetOrdersAsync(Utc(1, 0), Utc(30, 0)));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => _reports.GetFinancialSummaryAsync(Utc(1, 0), Utc(30, 0)));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => _reports.GetOutstandingBalancesAsync());
        await Assert.ThrowsAsync<PermissionDeniedException>(() => _reports.GetAwaitingPickupAsync());
    }

    [Fact]
    public async Task Reports_RequireAConnectedUser()
    {
        var anonymous = new ReportService(_ctx.Db, new CurrentUserContext());

        await Assert.ThrowsAsync<PermissionDeniedException>(() => anonymous.GetOrdersAsync(Utc(1, 0), Utc(30, 0)));
    }

    [Fact]
    public async Task GetOrders_CountsOnlyValidPayments_AndSortsNewestFirst()
    {
        var a = await _ctx.AddOrderAsync("A", Utc(5), OrderStatus.Cleaning, 5000m);
        await _ctx.AddOrderAsync("B", Utc(10), OrderStatus.Received, 1000m);
        await _ctx.AddOrderAsync("HORS-PERIODE", Utc(20), OrderStatus.Received, 999m);

        await _ctx.AddPaymentAsync(a, 2000m, Utc(5));
        await _ctx.AddPaymentAsync(a, 1000m, Utc(6));
        await _ctx.AddPaymentAsync(a, 500m, Utc(6), PaymentStatus.Cancelled);
        await _ctx.AddPaymentAsync(a, 700m, Utc(7), PaymentStatus.Refunded);

        var rows = await _reports.GetOrdersAsync(Utc(1, 0), Utc(15, 0));

        Assert.Equal(new[] { "B", "A" }, rows.Select(r => r.OrderNumber));
        var rowA = rows.Single(r => r.OrderNumber == "A");
        Assert.Equal(5000m, rowA.Total);
        Assert.Equal(3000m, rowA.AmountPaid);
        Assert.Equal(2000m, rowA.Balance);
        Assert.Equal("Awa Nkodo", rowA.CustomerName);
    }

    [Fact]
    public async Task GetOrders_FiltersByStatus()
    {
        await _ctx.AddOrderAsync("A", Utc(5), OrderStatus.Cleaning, 100m);
        await _ctx.AddOrderAsync("B", Utc(6), OrderStatus.Delivered, 100m);

        var rows = await _reports.GetOrdersAsync(Utc(1, 0), Utc(15, 0), OrderStatus.Delivered);

        Assert.Equal("B", Assert.Single(rows).OrderNumber);
    }

    [Fact]
    public async Task GetOrders_StartIsIncluded_EndIsExcluded()
    {
        await _ctx.AddOrderAsync("DEBUT", Utc(1, 0), OrderStatus.Received, 100m);
        await _ctx.AddOrderAsync("FIN", Utc(15, 0), OrderStatus.Received, 100m);

        var rows = await _reports.GetOrdersAsync(Utc(1, 0), Utc(15, 0));

        Assert.Equal("DEBUT", Assert.Single(rows).OrderNumber);
    }

    [Fact]
    public async Task GetOrders_EndBeforeStart_IsRefused()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _reports.GetOrdersAsync(Utc(15, 0), Utc(1, 0)));
    }

    [Fact]
    public async Task GetFinancialSummary_ExcludesCancelledOrdersPaymentsOutsideThePeriodAndCancelledExpenses()
    {
        var a = await _ctx.AddOrderAsync("A", Utc(5), OrderStatus.Cleaning, 5000m);
        await _ctx.AddOrderAsync("B", Utc(6), OrderStatus.Cancelled, 2000m);
        var c = await _ctx.AddOrderAsync("C", Utc(7), OrderStatus.Delivered, 1000m);

        await _ctx.AddPaymentAsync(a, 2000m, Utc(5));
        await _ctx.AddPaymentAsync(a, 1000m, Utc(5));
        await _ctx.AddPaymentAsync(a, 500m, Utc(20));   // après la période : pas dans « encaissé »
        await _ctx.AddPaymentAsync(c, 1000m, Utc(6));

        await _ctx.AddExpenseAsync(700m, Utc(7));
        await _ctx.AddExpenseAsync(300m, Utc(8), cancelled: true);
        await _ctx.AddExpenseAsync(400m, Utc(25));      // hors période

        var summary = await _reports.GetFinancialSummaryAsync(Utc(1, 0), Utc(15, 0));

        Assert.Equal(2, summary.OrderCount);          // la commande annulée est exclue
        Assert.Equal(6000m, summary.TotalBilled);
        Assert.Equal(4000m, summary.TotalCollected);  // 2000 + 1000 + 1000
        Assert.Equal(700m, summary.TotalExpenses);
        Assert.Equal(1500m, summary.OutstandingBalance); // A : 5000 - 3500
        Assert.Equal(3300m, summary.NetResult);
    }

    [Fact]
    public async Task GetOutstandingBalances_ListsUnpaidOrdersWithBiggestBalanceFirst()
    {
        var x = await _ctx.AddOrderAsync("X", Utc(1, month: 9), OrderStatus.Delivered, 1000m);
        await _ctx.AddOrderAsync("Y", Utc(2), OrderStatus.Received, 2000m);
        var z = await _ctx.AddOrderAsync("Z", Utc(3), OrderStatus.Delivered, 500m);
        await _ctx.AddOrderAsync("W", Utc(4), OrderStatus.Cancelled, 900m);

        await _ctx.AddPaymentAsync(x, 400m, Utc(1, month: 9));
        await _ctx.AddPaymentAsync(z, 500m, Utc(3));

        var rows = await _reports.GetOutstandingBalancesAsync();

        Assert.Equal(new[] { "Y", "X" }, rows.Select(r => r.OrderNumber));
        Assert.Equal(new[] { 2000m, 600m }, rows.Select(r => r.Balance));
    }

    [Fact]
    public async Task GetAwaitingPickup_ListsOnlyReadyOrders_OldestFirst()
    {
        await _ctx.AddOrderAsync("RECENT", Utc(10), OrderStatus.ReadyForPickup, 100m);
        await _ctx.AddOrderAsync("ANCIEN", Utc(2), OrderStatus.ReadyForPickup, 100m);
        await _ctx.AddOrderAsync("EN-COURS", Utc(3), OrderStatus.Ironing, 100m);
        await _ctx.AddOrderAsync("LIVRE", Utc(4), OrderStatus.Delivered, 100m);

        var rows = await _reports.GetAwaitingPickupAsync();

        Assert.Equal(new[] { "ANCIEN", "RECENT" }, rows.Select(r => r.OrderNumber));
    }
}
