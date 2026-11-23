using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;

namespace PressingManager.Tests.Unit;

public class DashboardCalculatorTests
{
    private static readonly DateTime DayStart = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DayEnd = DayStart.AddDays(1);

    private static OrderReportRow Order(string number, OrderStatus status, decimal total, decimal paid, DateTime createdAt) =>
        new(number, createdAt, "Client " + number, status, total, paid);

    private static Product ProductWithStock(int id, string name, decimal threshold, decimal stock, bool active = true)
    {
        var product = new Product { Id = id, Name = name, Unit = "kg", AlertThreshold = threshold, IsActive = active };
        product.StockMovements.Add(new StockMovement
        {
            ProductId = id,
            Type = StockMovementType.Entry,
            Quantity = stock,
            MovementDate = DayStart
        });
        return product;
    }

    [Fact]
    public void GetLocalDayRangeUtc_FollowsTheLocalTimeZone()
    {
        var utcPlusOne = TimeZoneInfo.CreateCustomTimeZone("UTC+1", TimeSpan.FromHours(1), "UTC+1", "UTC+1");

        // 23h30 UTC = 00h30 le lendemain a UTC+1 : la journee locale est donc le 24.
        var (start, end) = DashboardCalculator.GetLocalDayRangeUtc(
            new DateTime(2026, 11, 23, 23, 30, 0, DateTimeKind.Utc), utcPlusOne);

        Assert.Equal(new DateTime(2026, 11, 23, 23, 0, 0), start);
        Assert.Equal(new DateTime(2026, 11, 24, 23, 0, 0), end);
    }

    [Fact]
    public void Summarize_CountsOrdersByCategory_AndIgnoresCancelled()
    {
        var yesterday = DayStart.AddHours(-5);
        var today = DayStart.AddHours(9);

        var summary = DashboardCalculator.Summarize(
            new[]
            {
                Order("A", OrderStatus.Received, 1000m, 0m, today),
                Order("B", OrderStatus.Cleaning, 2000m, 500m, yesterday),
                Order("C", OrderStatus.ReadyForPickup, 3000m, 3000m, today),
                Order("D", OrderStatus.Delivered, 4000m, 4000m, yesterday),
                Order("E", OrderStatus.Cancelled, 9000m, 0m, today)
            },
            collectedToday: 3500m,
            products: Array.Empty<Product>(),
            DayStart,
            DayEnd);

        Assert.Equal(2, summary.OrdersToday);       // A et C (E est annulee)
        Assert.Equal(2, summary.OrdersInProgress);  // A et B
        Assert.Equal(1, summary.OrdersReady);       // C
        Assert.Equal(3500m, summary.RevenueToday);
        Assert.Equal(2, summary.UnpaidOrderCount);  // A et B
        Assert.Equal(2500m, summary.OutstandingBalance);
    }

    [Fact]
    public void Summarize_DayEndIsExclusive()
    {
        var summary = DashboardCalculator.Summarize(
            new[]
            {
                Order("A", OrderStatus.Received, 100m, 0m, DayStart),
                Order("B", OrderStatus.Received, 100m, 0m, DayEnd)
            },
            0m, Array.Empty<Product>(), DayStart, DayEnd);

        Assert.Equal(1, summary.OrdersToday);
    }

    [Fact]
    public void GetStockAlerts_ReturnsActiveProductsAtOrBelowThreshold_LowestFirst()
    {
        var alerts = DashboardCalculator.GetStockAlerts(new[]
        {
            ProductWithStock(1, "Détergent", threshold: 10m, stock: 10m),   // egal au seuil : alerte
            ProductWithStock(2, "Cintres", threshold: 50m, stock: 5m),      // alerte, plus bas
            ProductWithStock(3, "Sachets", threshold: 20m, stock: 100m),    // OK
            ProductWithStock(4, "Ancien produit", threshold: 10m, stock: 0m, active: false)
        });

        Assert.Equal(new[] { "Cintres", "Détergent" }, alerts.Select(a => a.ProductName));
        Assert.Equal(5m, alerts[0].CurrentStock);
    }

    [Fact]
    public void GetStockAlerts_SubtractsExitsFromTheStock()
    {
        var product = ProductWithStock(1, "Détergent", threshold: 10m, stock: 30m);
        product.StockMovements.Add(new StockMovement { Type = StockMovementType.Exit, Quantity = 25m });

        var alert = Assert.Single(DashboardCalculator.GetStockAlerts(new[] { product }));

        Assert.Equal(5m, alert.CurrentStock);
    }
}
