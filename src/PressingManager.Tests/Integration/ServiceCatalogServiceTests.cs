using PressingManager.Core.Common;
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
    private readonly int _userId;

    public ServiceCatalogServiceTests()
    {
        _userId = _db.AddUser();
    }

    public void Dispose() => _db.Dispose();

    private ServiceCatalogService CreateService()
    {
        var session = ProcessingTestSession.SignedIn(userId: _userId);
        return new(_db, session, new AuditService(_db, session, _clock), _clock);
    }

    private int CountPriceAuditLogs()
    {
        using var db = _db.CreateDbContext();
        return db.AuditLogs.Count(a => a.Action == ServiceCatalogService.PriceChangedAuditAction);
    }

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

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(new CreateServiceDto("REPASSAGE", null)));
    }

    [Fact]
    public async Task CreateAsync_EmptyName_IsRefused()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessRuleException>(
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

        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Kilogram, 500m, _userId));
        _clock.Advance(TimeSpan.FromDays(1));
        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Kilogram, 600m, _userId));

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

        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Piece, 200m, _userId));
        _clock.Advance(TimeSpan.FromHours(1));
        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Piece, 200m, _userId));

        var loaded = await service.GetByIdAsync(created.Id);
        Assert.Single(loaded!.Prices);
        Assert.Equal(1, CountPriceAuditLogs());
    }

    [Fact]
    public async Task SetPriceAsync_EachChange_IsWrittenToTheAuditLog()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Kilogram, 500m, _userId));
        _clock.Advance(TimeSpan.FromHours(1));
        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Kilogram, 650m, _userId));

        Assert.Equal(2, CountPriceAuditLogs());
    }

    [Fact]
    public async Task SetPriceAsync_UnitsAreIndependent()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Kilogram, 500m, _userId));
        _clock.Advance(TimeSpan.FromHours(1));
        await service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Piece, 300m, _userId));

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

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Piece, amount, _userId)));
    }

    [Fact]
    public async Task SetPriceAsync_UnknownService_IsRefused()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SetPriceAsync(new SetServicePriceDto(999, BillingUnit.Piece, 100m, _userId)));
    }

    [Fact]
    public async Task SetPriceAsync_UnknownUser_IsRefused()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SetPriceAsync(new SetServicePriceDto(created.Id, BillingUnit.Piece, 100m, 9999)));
    }

    [Fact]
    public async Task GetCurrentPriceAsync_WithoutPrice_ReturnsNull()
    {
        var service = CreateService();
        var created = await service.CreateAsync(new CreateServiceDto("Lavage", null));

        Assert.Null(await service.GetCurrentPriceAsync(created.Id, BillingUnit.Piece));
    }
}
