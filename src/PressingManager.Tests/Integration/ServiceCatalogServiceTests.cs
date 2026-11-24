using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

public class ServiceCatalogServiceTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly ProcessingTestDb _db = new();
    private readonly ProcessingTestClock _clock = new(Start);

    public void Dispose() => _db.Dispose();

    private ServiceCatalogService CreateService() => new(_db.CreateContext(), _clock);

    [Fact]
    public async Task CreateAsync_ThenGetById_ReturnsTheService()
    {
        var service = CreateService();

        var created = await service.CreateAsync(new CreateServiceDto("  Lavage  ", "Lavage du linge"));
        var loaded = await service.GetByIdAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Lavage", loaded!.Name);
        Assert.True(loaded.IsActive);
        Assert.Empty(loaded.Prices);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_IgnoringCase_IsRefused()
    {
        var service = CreateService();
        await service.CreateAsync(new CreateServiceDto("Repassage", null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(new CreateServiceDto("REPASSAGE", null)));
    }

    [Fact]
    public async Task CreateAsync_EmptyName_IsRefused()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(new CreateServiceDto("   ", null)));
    }

    [Fact]
    public async Task UpdateAsync_ChangesNameAndDescription()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        var updated = await service.UpdateAsync(new UpdateServiceDto(created.Id, "Lavage express", "Rapide"));

        Assert.Equal("Lavage express", updated.Name);
        Assert.Equal("Rapide", updated.Description);
    }

    [Fact]
    public async Task SetActiveAsync_Deactivated_IsHiddenByDefault()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        await service.SetActiveAsync(created.Id, false);

        Assert.Empty(await service.GetServicesAsync());
        Assert.Single(await service.GetServicesAsync(includeInactive: true));
    }

    [Fact]
    public async Task SetPriceAsync_NewPrice_ClosesThePreviousOneAndKeepsHistory()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Kilogram, 500m, 1));
        _clock.Advance(TimeSpan.FromDays(1));
        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Kilogram, 600m, 1));

        var now = await service.GetCurrentPriceAsync(created.Id, BillingUnit.Kilogram);
        var yesterday = await service.GetCurrentPriceAsync(
            created.Id, BillingUnit.Kilogram, Start.UtcDateTime.AddHours(1));
        var loaded = await service.GetByIdAsync(created.Id);

        Assert.Equal(600m, now!.Amount);
        Assert.Equal(500m, yesterday!.Amount);
        Assert.Equal(2, loaded!.Prices.Count);
        Assert.Single(loaded.Prices, p => p.ValidTo is null);
    }

    [Fact]
    public async Task SetPriceAsync_SameAmount_DoesNotCreateAnotherPrice()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Piece, 200m, 1));
        _clock.Advance(TimeSpan.FromHours(1));
        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Piece, 200m, 1));

        var loaded = await service.GetByIdAsync(created.Id);
        Assert.Single(loaded!.Prices);
    }

    [Fact]
    public async Task SetPriceAsync_UnitsAreIndependent()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Kilogram, 500m, 1));
        _clock.Advance(TimeSpan.FromHours(1));
        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Piece, 300m, 1));

        Assert.Equal(500m, (await service.GetCurrentPriceAsync(created.Id, BillingUnit.Kilogram))!.Amount);
        Assert.Equal(300m, (await service.GetCurrentPriceAsync(created.Id, BillingUnit.Piece))!.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task SetPriceAsync_NonPositiveAmount_IsRefused(int amount)
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Piece, amount, 1)));
    }

    [Fact]
    public async Task SetPriceAsync_UnknownService_IsRefused()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetPriceAsync(new SetServicePriceDto(999, BillingUnit.Piece, 100m, 1)));
    }

    [Fact]
    public async Task GetCurrentPriceAsync_WithoutPrice_ReturnsNull()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        Assert.Null(await service.GetCurrentPriceAsync(created.Id, BillingUnit.Piece));
    }
}
