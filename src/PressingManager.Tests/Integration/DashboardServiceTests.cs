using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Unit;
using static PressingManager.Tests.Integration.ReportTestContext;

namespace PressingManager.Tests.Integration;

public sealed class DashboardServiceTests : IDisposable
{
    private readonly ReportTestContext _ctx = new();

    // « Maintenant » = 23/11/2026 10h UTC ; fuseau UTC pour que le test ne dépende pas de la machine.
    private readonly TestTimeProvider _time = new(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero));

    public void Dispose() => _ctx.Dispose();

    private DashboardService CreateService(IStockService? stock = null) =>
        new(_ctx.Db, _ctx.Session, _time, stock, TimeZoneInfo.Utc);

    [Fact]
    public async Task GetSummary_ComputesTheDailyIndicators()
    {
        await _ctx.AddOrderAsync("AUJ-1", Utc(23, 8), OrderStatus.Received, 1000m);
        var ready = await _ctx.AddOrderAsync("AUJ-2", Utc(23, 9), OrderStatus.ReadyForPickup, 2000m);
        var yesterday = await _ctx.AddOrderAsync("HIER", Utc(22, 15), OrderStatus.Cleaning, 3000m);
        await _ctx.AddOrderAsync("ANNULEE", Utc(23, 7), OrderStatus.Cancelled, 700m);
        var delivered = await _ctx.AddOrderAsync("LIVREE", Utc(20), OrderStatus.Delivered, 800m);

        await _ctx.AddPaymentAsync(ready, 500m, Utc(23, 9));
        await _ctx.AddPaymentAsync(yesterday, 3000m, Utc(22, 16));
        await _ctx.AddPaymentAsync(delivered, 300m, Utc(21));

        var summary = await CreateService().GetSummaryAsync();

        Assert.Equal(2, summary.OrdersToday);          // annulée exclue
        Assert.Equal(2, summary.OrdersInProgress);     // reçue + en nettoyage
        Assert.Equal(1, summary.OrdersReady);
        Assert.Equal(500m, summary.RevenueToday);      // seul le paiement d'aujourd'hui
        Assert.Equal(3, summary.UnpaidOrderCount);     // AUJ-1, AUJ-2, LIVREE
        Assert.Equal(3000m, summary.OutstandingBalance); // 1000 + 1500 + 500
        Assert.Empty(summary.StockAlerts);
    }

    [Fact]
    public async Task GetSummary_RequiresTheViewDashboardPermission()
    {
        _ctx.SignInAs(UserRole.Processor);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => CreateService().GetSummaryAsync());
    }

    [Fact]
    public async Task GetSummary_UsesStockAlertsFromTheStockModule()
    {
        var stock = new FakeStockService(() => new[]
        {
            new ProductDto(2, "Cintres", "unité", 50m, true, 40m, true),
            new ProductDto(1, "Détergent", "L", 5m, true, 2m, true)
        });

        var summary = await CreateService(stock).GetSummaryAsync();

        Assert.Equal(new[] { "Détergent", "Cintres" }, summary.StockAlerts.Select(a => a.ProductName)); // stock le plus bas d'abord
        Assert.Equal(new StockAlert(1, "Détergent", "L", 2m, 5m), summary.StockAlerts[0]);
    }

    [Fact]
    public async Task GetSummary_WhenUserCannotSeeStock_ReturnsTheRestWithoutAlerts()
    {
        _ctx.SignInAs(UserRole.Cashier); // a ViewDashboard, pas ViewStock
        await _ctx.AddOrderAsync("AUJ", Utc(23, 8), OrderStatus.Received, 1000m);

        var stock = new FakeStockService(() => throw new PermissionDeniedException("Interdit."));

        var summary = await CreateService(stock).GetSummaryAsync();

        Assert.Equal(1, summary.OrdersToday);
        Assert.Empty(summary.StockAlerts);
    }

    private sealed class FakeStockService : IStockService
    {
        private readonly Func<IReadOnlyList<ProductDto>> _lowStock;

        public FakeStockService(Func<IReadOnlyList<ProductDto>> lowStock) => _lowStock = lowStock;

        public Task<IReadOnlyList<ProductDto>> GetLowStockProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_lowStock());

        public Task<IReadOnlyList<ProductDto>> GetProductsAsync(bool includeInactive = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProductDto?> GetProductByIdAsync(int productId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProductDto> CreateProductAsync(CreateProductDto request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProductDto> UpdateProductAsync(UpdateProductDto request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StockMovementDto> RecordMovementAsync(RecordStockMovementDto request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(int productId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
