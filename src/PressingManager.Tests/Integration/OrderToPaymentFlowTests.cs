using System.Text;
using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Data.Seed;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

/// <summary>
/// Verifie le flux complet depuis la creation client/commande jusqu'a l'encaissement et le recu PDF.
/// </summary>
public sealed class OrderToPaymentFlowTests : IDisposable
{
    private readonly ProcessingTestDb _db = new();
    private readonly ProcessingTestClock _clock = new(new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero));
    private readonly int _userId;

    public OrderToPaymentFlowTests()
    {
        _userId = _db.AddUser();
        using var context = _db.CreateDbContext();
        ProcessingSeed.Seed(context, _clock);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateOrder_AppearsInProcessingQueue_AndCanBePaidInFullWithPdfReceipt()
    {
        var session = ProcessingTestSession.SignedIn(UserRole.Administrator, _userId);
        var audit = new AuditService(_db, session, _clock);

        var customerService = new CustomerService(_db, session, _clock);
        var catalogService = new ServiceCatalogService(_db, session, audit, _clock);
        var orderService = new OrderService(_db, session, _clock);
        var processingService = new OrderProcessingService(_db, session, audit, _clock);
        var cashService = new CashService(_db, session, audit, _clock);
        var paymentService = new PaymentService(_db, session, audit, _clock);

        // 1. Client & catalogue de services seedes
        var customer = await customerService.CreateAsync(new CreateCustomerDto("Jean Dupont", "690112233"));
        var services = await catalogService.GetServicesAsync();
        Assert.True(services.Count >= 2);

        // 2. Creation d'une commande multi-articles avec remise et frais
        var expectedDate = _clock.GetLocalNow().Date.AddDays(2);
        var createdOrder = await orderService.CreateAsync(new CreateOrderDto(
            customer.Id,
            [
                new CreateOrderItemDto(services[0].Id, "Chemise", 3, 500m, "Coton"),
                new CreateOrderItemDto(services[1].Id, "Costume", 1, 1500m)
            ],
            ExpectedDate: expectedDate,
            Discount: 200m,
            Fees: 300m,
            Notes: "Livraison urgente"));

        // Sous-total = 3*500 + 1500 = 3000 ; Total = 3000 - 200 + 300 = 3100
        Assert.Equal(3000m, createdOrder.Subtotal);
        Assert.Equal(3100m, createdOrder.Total);
        Assert.StartsWith("CMD-", createdOrder.OrderNumber);

        // 3. Visibilite dans la file Commandes & Traitement + recherche par numero
        var queue = await processingService.GetOrdersAsync();
        var queued = Assert.Single(queue, o => o.OrderId == createdOrder.Id);
        Assert.Equal("Jean Dupont", queued.CustomerName);
        Assert.Equal(4, queued.ItemCount);

        var byNumber = await orderService.GetByNumberAsync(createdOrder.OrderNumber);
        Assert.NotNull(byNumber);
        Assert.Equal(createdOrder.Id, byNumber.Id);

        // 4. Ouverture de caisse et encaissement en 2 temps (especes puis Mobile Money)
        await cashService.OpenAsync(new OpenCashSessionDto(5000m, _userId));

        await paymentService.RegisterAsync(new RegisterPaymentDto(
            createdOrder.Id,
            1000m,
            PaymentMethod.Cash,
            _userId));

        Assert.Equal(2100m, await paymentService.GetBalanceAsync(createdOrder.Id));

        var finalPayment = await paymentService.RegisterAsync(new RegisterPaymentDto(
            createdOrder.Id,
            2100m,
            PaymentMethod.MtnMobileMoney,
            _userId,
            "MTN-20261125-001"));

        Assert.Equal(0m, await paymentService.GetBalanceAsync(createdOrder.Id));

        // 5. Generation du recu officiel et PDF
        var receipt = PaymentReceiptGenerator.BuildReceipt(
            finalPayment,
            createdOrder.OrderNumber,
            createdOrder.CustomerName,
            createdOrder.Total,
            totalPaidOnOrder: 3100m,
            cashierName: "Utilisateur test");

        Assert.True(receipt.IsFullyPaid);

        var pdfBytes = PaymentReceiptPdfBuilder.BuildReceiptPdfBytes(receipt);
        Assert.True(pdfBytes.Length > 500);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }
}
