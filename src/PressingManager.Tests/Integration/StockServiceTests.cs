using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

public class StockServiceTests : IDisposable
{
    private readonly ProcessingTestDb _db = new();
    private readonly ProcessingTestClock _clock = new(new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly int _userId;

    public StockServiceTests()
    {
        _userId = _db.AddUser();
    }

    public void Dispose() => _db.Dispose();

    private StockService CreateService() => new(_db, _clock);

    private static Task<ProductDto> NewProduct(StockService service, string name = "Détergent", decimal threshold = 5m) =>
        service.CreateProductAsync(new CreateProductDto(name, "kg", threshold));

    private RecordStockMovementDto Movement(int productId, StockMovementType type, decimal quantity, string? reason = null) =>
        new(productId, type, quantity, _userId, reason);

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

        await Assert.ThrowsAsync<BusinessRuleException>(() => NewProduct(service, "cintres"));
    }

    [Fact]
    public async Task CreateProductAsync_NegativeThreshold_IsRefused()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => NewProduct(service, "Cintres", -1m));
    }

    [Fact]
    public async Task RecordMovementAsync_EntryThenExit_UpdatesStock()
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await service.RecordMovementAsync(Movement(product.Id, StockMovementType.Entry, 10m, "Achat"));
        await service.RecordMovementAsync(Movement(product.Id, StockMovementType.Exit, 3m));

        var loaded = await service.GetProductByIdAsync(product.Id);
        Assert.Equal(7m, loaded!.CurrentStock);
        Assert.False(loaded.IsBelowAlertThreshold);
    }

    [Fact]
    public async Task RecordMovementAsync_FractionalQuantities_AreKept()
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await service.RecordMovementAsync(Movement(product.Id, StockMovementType.Entry, 2.5m));
        await service.RecordMovementAsync(Movement(product.Id, StockMovementType.Exit, 1.25m));

        Assert.Equal(1.25m, (await service.GetProductByIdAsync(product.Id))!.CurrentStock);
    }

    [Theory]
    [InlineData(StockMovementType.Exit)]
    [InlineData(StockMovementType.AdjustmentDecrease)]
    public async Task RecordMovementAsync_RemovingMoreThanAvailable_IsRefused(StockMovementType type)
    {
        var service = CreateService();
        var product = await NewProduct(service);
        await service.RecordMovementAsync(Movement(product.Id, StockMovementType.Entry, 4m));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RecordMovementAsync(Movement(product.Id, type, 5m)));

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

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RecordMovementAsync(Movement(product.Id, StockMovementType.Entry, quantity)));
    }

    [Fact]
    public async Task RecordMovementAsync_UnknownProduct_IsRefused()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RecordMovementAsync(Movement(999, StockMovementType.Entry, 1m)));
    }

    [Fact]
    public async Task RecordMovementAsync_UnknownUser_IsRefused()
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Entry, 1m, 9999)));
    }

    [Fact]
    public async Task AdjustmentIncrease_AddsToStock()
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await service.RecordMovementAsync(Movement(product.Id, StockMovementType.AdjustmentIncrease, 8m, "Inventaire"));

        Assert.Equal(8m, (await service.GetProductByIdAsync(product.Id))!.CurrentStock);
    }

    [Fact]
    public async Task GetMovementsAsync_ReturnsMostRecentFirst()
    {
        var service = CreateService();
        var product = await NewProduct(service);

        await service.RecordMovementAsync(Movement(product.Id, StockMovementType.Entry, 10m, "premier"));
        _clock.Advance(TimeSpan.FromMinutes(5));
        await service.RecordMovementAsync(Movement(product.Id, StockMovementType.Exit, 2m, "second"));

        var movements = await service.GetMovementsAsync(product.Id);

        Assert.Equal(new[] { "second", "premier" }, movements.Select(m => m.Reason).ToArray());
    }

    [Fact]
    public async Task GetLowStockProductsAsync_ReturnsOnlyProductsAtOrBelowThreshold()
    {
        var service = CreateService();
        var low = await NewProduct(service, "Bas", 5m);
        var atThreshold = await NewProduct(service, "Au seuil", 5m);
        var fine = await NewProduct(service, "Bon", 5m);

        await service.RecordMovementAsync(Movement(low.Id, StockMovementType.Entry, 2m));
        await service.RecordMovementAsync(Movement(atThreshold.Id, StockMovementType.Entry, 5m));
        await service.RecordMovementAsync(Movement(fine.Id, StockMovementType.Entry, 20m));

        var alerts = await service.GetLowStockProductsAsync();

        Assert.Equal(new[] { "Au seuil", "Bas" }, alerts.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task UpdateProductAsync_ChangesThreshold_AndRecomputesAlert()
    {
        var service = CreateService();
        var product = await NewProduct(service, "Détergent", 5m);
        await service.RecordMovementAsync(Movement(product.Id, StockMovementType.Entry, 10m));

        var updated = await service.UpdateProductAsync(new UpdateProductDto(product.Id, "Détergent", "kg", 12m));

        Assert.Equal(12m, updated.AlertThreshold);
        Assert.True(updated.IsBelowAlertThreshold);
    }
}
