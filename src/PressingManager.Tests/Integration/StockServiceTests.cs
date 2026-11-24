using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

public class StockServiceTests : IDisposable
{
    private readonly ProcessingTestDb _db = new();
    private readonly ProcessingTestClock _clock = new(new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero));

    public void Dispose() => _db.Dispose();

    private StockService CreateService() => new(_db.CreateContext(), _clock);

    private static Task<ProductDto> NewProduct(StockService service, string name = "Détergent", decimal threshold = 5m) =>
        service.CreateProductAsync(new CreateProductDto(name, "kg", threshold));

    [Fact]
    public async Task CreateProductAsync_StartsWithZeroStock_AndIsInAlert()
    {
        var service = CreateService();

        var product = await NewProduct(service);

        Assert.Equal(0m, product.CurrentStock);
        Assert.True(product.IsBelowAlertThreshold);
    }

    [Fact]
    public async Task CreateProductAsync_DuplicateName_IsRefused()
    {
        var service = CreateService();
        await NewProduct(service, "Cintres");

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewProduct(service, "cintres"));
    }

    [Fact]
    public async Task CreateProductAsync_NegativeThreshold_IsRefused()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => NewProduct(service, "Cintres", -1m));
    }

    [Fact]
    public async Task RecordMovementAsync_EntryThenExit_UpdatesStock()
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Entry, 10m, 1, "Achat"));
        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Exit, 3m, 1));

        var loaded = await service.GetProductByIdAsync(product.Id);
        Assert.Equal(7m, loaded!.CurrentStock);
        Assert.False(loaded.IsBelowAlertThreshold);
    }

    [Fact]
    public async Task RecordMovementAsync_FractionalQuantities_AreKept()
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Entry, 2.5m, 1));
        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Exit, 1.25m, 1));

        Assert.Equal(1.25m, (await service.GetProductByIdAsync(product.Id))!.CurrentStock);
    }

    [Theory]
    [InlineData(StockMovementType.Exit)]
    [InlineData(StockMovementType.AdjustmentDecrease)]
    public async Task RecordMovementAsync_RemovingMoreThanAvailable_IsRefused(StockMovementType type)
    {
        var service = CreateService();
        var product = await NewProduct(service);
        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Entry, 4m, 1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecordMovementAsync(new RecordStockMovementDto(product.Id, type, 5m, 1)));

        Assert.Equal(4m, (await service.GetProductByIdAsync(product.Id))!.CurrentStock);
        Assert.Single(await service.GetMovementsAsync(product.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task RecordMovementAsync_NonPositiveQuantity_IsRefused(int quantity)
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Entry, quantity, 1)));
    }

    [Fact]
    public async Task RecordMovementAsync_UnknownProduct_IsRefused()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RecordMovementAsync(new RecordStockMovementDto(999, StockMovementType.Entry, 1m, 1)));
    }

    [Fact]
    public async Task AdjustmentIncrease_AddsToStock()
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.AdjustmentIncrease, 8m, 1, "Inventaire"));

        Assert.Equal(8m, (await service.GetProductByIdAsync(product.Id))!.CurrentStock);
    }

    [Fact]
    public async Task GetMovementsAsync_ReturnsMostRecentFirst()
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Entry, 10m, 1, "premier"));
        _clock.Advance(TimeSpan.FromMinutes(5));
        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Exit, 2m, 1, "second"));

        var movements = await service.GetMovementsAsync(product.Id);

        Assert.Equal(new[] { "second", "premier" }, movements.Select(m => m.Reason).ToArray());
    }

    [Fact]
    public async Task GetLowStockProductsAsync_ReturnsOnlyActiveProductsAtOrBelowThreshold()
    {
        var service = CreateService();
        var low = await NewProduct(service, "Bas", 5m);
        var atThreshold = await NewProduct(service, "Au seuil", 5m);
        var fine = await NewProduct(service, "Bon", 5m);

        await service.RecordMovementAsync(new RecordStockMovementDto(low.Id, StockMovementType.Entry, 2m, 1));
        await service.RecordMovementAsync(new RecordStockMovementDto(atThreshold.Id, StockMovementType.Entry, 5m, 1));
        await service.RecordMovementAsync(new RecordStockMovementDto(fine.Id, StockMovementType.Entry, 20m, 1));

        var alerts = await service.GetLowStockProductsAsync();

        Assert.Equal(new[] { "Au seuil", "Bas" }, alerts.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task UpdateProductAsync_ChangesThreshold_AndRecomputesAlert()
    {
        var service = CreateService();
        var product = await NewProduct(service, "Détergent", 5m);
        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Entry, 10m, 1));

        var updated = await service.UpdateProductAsync(new UpdateProductDto(product.Id, "Détergent", "kg", 12m));

        Assert.Equal(12m, updated.AlertThreshold);
        Assert.True(updated.IsBelowAlertThreshold);
    }
}
