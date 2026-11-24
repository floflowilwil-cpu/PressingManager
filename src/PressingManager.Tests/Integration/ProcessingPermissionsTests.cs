using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

/// <summary>Permissions et audit du module traitement / services / stocks (documentation, section 11).</summary>
public class ProcessingPermissionsTests : IDisposable
{
    private readonly ProcessingTestDb _db = new();
    private readonly ProcessingTestClock _clock = new(new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero));
    private readonly int _userId;

    public ProcessingPermissionsTests()
    {
        _userId = _db.AddUser();
    }

    public void Dispose() => _db.Dispose();

    private OrderProcessingService Processing(CurrentUserContext session) =>
        new(_db, session, new AuditService(_db, session, _clock), _clock);

    private ServiceCatalogService Catalog(CurrentUserContext session) =>
        new(_db, session, new AuditService(_db, session, _clock), _clock);

    private StockService Stock(CurrentUserContext session) => new(_db, session, _clock);

    private int CountAudit(string action)
    {
        using var db = _db.CreateDbContext();
        return db.AuditLogs.Count(a => a.Action == action);
    }

    // ---------- Catalogue ----------

    [Theory]
    [InlineData(UserRole.FrontDesk)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Processor)]
    [InlineData(UserRole.StockManager)]
    public async Task Catalog_Writes_RequireManageServices(UserRole role)
    {
        var service = Catalog(ProcessingTestSession.SignedIn(role, _userId));

        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => service.CreateAsync(new CreateServiceDto("Lavage", null)));
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => service.SetPriceAsync(new SetServicePriceDto(1, BillingUnit.Piece, 100m, _userId)));
    }

    [Fact]
    public async Task Catalog_Reads_AreOpenToEveryRole()
    {
        var admin = Catalog(ProcessingTestSession.SignedIn(UserRole.Administrator, _userId));
        var created = await admin.CreateAsync(new CreateServiceDto("Lavage", null));

        var frontDesk = Catalog(ProcessingTestSession.SignedIn(UserRole.FrontDesk, _userId));

        Assert.Single(await frontDesk.GetServicesAsync());
        Assert.NotNull(await frontDesk.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task Catalog_SignedOut_IsDenied()
    {
        var service = Catalog(ProcessingTestSession.SignedOut());

        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => service.CreateAsync(new CreateServiceDto("Lavage", null)));
    }

    // ---------- Stock ----------

    [Theory]
    [InlineData(UserRole.FrontDesk)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Processor)]
    public async Task Stock_Everything_IsDeniedWithoutStockPermissions(UserRole role)
    {
        var service = Stock(ProcessingTestSession.SignedIn(role, _userId));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetProductsAsync());
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetLowStockProductsAsync());
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => service.CreateProductAsync(new CreateProductDto("Détergent", "kg", 5m)));
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => service.RecordMovementAsync(new RecordStockMovementDto(1, StockMovementType.Entry, 1m, _userId)));
    }

    [Fact]
    public async Task Stock_StockManager_CanManageAndView()
    {
        var service = Stock(ProcessingTestSession.SignedIn(UserRole.StockManager, _userId));

        var product = await service.CreateProductAsync(new CreateProductDto("Détergent", "kg", 5m));
        await service.RecordMovementAsync(new RecordStockMovementDto(product.Id, StockMovementType.Entry, 10m, _userId));

        var loaded = await service.GetProductByIdAsync(product.Id);

        Assert.Equal(10m, loaded!.CurrentStock);
    }

    // ---------- Statuts ----------

    [Theory]
    [InlineData(UserRole.FrontDesk)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.StockManager)]
    public async Task ChangeStatus_WithoutUpdateOrderStatus_IsDenied(UserRole role)
    {
        var orderId = _db.AddOrder();
        var service = Processing(ProcessingTestSession.SignedIn(role, _userId));

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            service.ChangeStatusAsync(new ChangeOrderStatusDto(orderId, OrderStatus.AwaitingProcessing, _userId)));
    }

    [Fact]
    public async Task ChangeStatus_Processor_CanDoTheNormalFlow()
    {
        var orderId = _db.AddOrder();
        var service = Processing(ProcessingTestSession.SignedIn(UserRole.Processor, _userId));

        var entry = await service.ChangeStatusAsync(
            new ChangeOrderStatusDto(orderId, OrderStatus.AwaitingProcessing, _userId));

        Assert.Equal(OrderStatus.AwaitingProcessing, entry.ToStatus);
    }

    [Fact]
    public async Task ChangeStatus_Correction_NeedsCorrectOrderStatus_AndChangesNothingWhenDenied()
    {
        var orderId = _db.AddOrder(OrderStatus.Delivered);
        var processor = Processing(ProcessingTestSession.SignedIn(UserRole.Processor, _userId));

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            processor.ChangeStatusAsync(new ChangeOrderStatusDto(
                orderId, OrderStatus.ReadyForPickup, _userId, IsAuthorizedCorrection: true)));

        using var db = _db.CreateDbContext();
        Assert.Equal(OrderStatus.Delivered, db.Orders.AsNoTracking().Single(o => o.Id == orderId).Status);
        Assert.Empty(db.OrderStatusHistory.AsNoTracking().Where(h => h.OrderId == orderId));
    }

    [Fact]
    public async Task ChangeStatus_Correction_ByAdministrator_IsAudited()
    {
        var orderId = _db.AddOrder(OrderStatus.Delivered);
        var admin = Processing(ProcessingTestSession.SignedIn(UserRole.Administrator, _userId));

        await admin.ChangeStatusAsync(new ChangeOrderStatusDto(
            orderId, OrderStatus.ReadyForPickup, _userId, IsAuthorizedCorrection: true, Comment: "Livré par erreur"));

        Assert.Equal(1, CountAudit(AuditActions.OrderStatusCorrected));
    }

    [Fact]
    public async Task ChangeStatus_Cancellation_IsAudited()
    {
        var orderId = _db.AddOrder();
        var service = Processing(ProcessingTestSession.SignedIn(UserRole.Administrator, _userId));

        await service.ChangeStatusAsync(new ChangeOrderStatusDto(orderId, OrderStatus.Cancelled, _userId));

        Assert.Equal(1, CountAudit(AuditActions.OrderCancelled));
        Assert.Equal(0, CountAudit(AuditActions.OrderStatusCorrected));
    }

    [Fact]
    public async Task ChangeStatus_NormalTransition_WritesNoAuditEntry()
    {
        var orderId = _db.AddOrder();
        var service = Processing(ProcessingTestSession.SignedIn(UserRole.Administrator, _userId));

        await service.ChangeStatusAsync(new ChangeOrderStatusDto(orderId, OrderStatus.AwaitingProcessing, _userId));

        Assert.Equal(0, CountAudit(AuditActions.OrderCancelled));
        Assert.Equal(0, CountAudit(AuditActions.OrderStatusCorrected));
    }
}
