using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Unit;

namespace PressingManager.Tests.Integration;

public sealed class PaymentAndCashIntegrationTests : IDisposable
{
    private readonly AuthTestContext _auth = new();
    private readonly PaymentService _payments;
    private readonly ExpenseService _expenses;
    private readonly CashService _cash;

    public PaymentAndCashIntegrationTests()
    {
        _payments = new PaymentService(_auth.Db, _auth.Session, _auth.Audit, _auth.Time);
        _expenses = new ExpenseService(_auth.Db, _auth.Session, _auth.Audit, _auth.Time);
        _cash = new CashService(_auth.Db, _auth.Session, _auth.Audit, _auth.Time);
    }

    public void Dispose() => _auth.Dispose();

    private async Task<Order> SeedOrderAsync(decimal itemTotal, decimal discount = 0m, decimal fees = 0m)
    {
        await using var db = _auth.Db.CreateDbContext();
        var customer = new Customer
        {
            CustomerNumber = $"CLI-{Guid.NewGuid():N}"[..12],
            FullName = "Jean Client",
            Phone = "670000000"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var order = new Order
        {
            OrderNumber = $"CMD-{Guid.NewGuid():N}"[..12],
            CustomerId = customer.Id,
            Discount = discount,
            Fees = fees,
            Items =
            {
                new OrderItem
                {
                    ServiceId = 1,
                    ItemType = "Costume",
                    Quantity = 1,
                    UnitPrice = itemTotal
                }
            }
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task Cashier_CanOpenCashRegisterPaymentsAndClose_ButCannotRefundOrManageExpenses()
    {
        await _auth.SeedAdminAndSignInAsync();
        await _auth.Users.CreateAsync(new CreateUserDto("Paul Caissier", "paul", "Secret123", UserRole.Cashier));
        await _auth.Auth.LogoutAsync();

        var login = await _auth.Auth.LoginAsync("paul", "Secret123");
        Assert.True(login.IsSuccess);

        var order = await SeedOrderAsync(12000m, discount: 2000m); // Total = 10000
        var session = await _cash.OpenAsync(new OpenCashSessionDto(5000m, 0));

        var p1 = await _payments.RegisterAsync(new RegisterPaymentDto(order.Id, 4000m, PaymentMethod.Cash, 0));
        var p2 = await _payments.RegisterAsync(new RegisterPaymentDto(order.Id, 3500m, PaymentMethod.MtnMobileMoney, 0, "MTN-SQL-01"));

        var remaining = await _payments.GetBalanceAsync(order.Id);
        Assert.Equal(2500m, remaining);
        Assert.Equal(login.User!.Id, p1.ReceivedByUserId);

        // Caissier refuse sur remboursement, annulation et depenses
        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            _payments.CancelAsync(new CancelPaymentDto(p1.Id, "Test", 0)));
        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            _payments.RefundAsync(new CancelPaymentDto(p2.Id, "Test", 0)));
        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            _expenses.CreateAsync(new CreateExpenseDto(DateTime.UtcNow, "Detergent", 1000m, PaymentMethod.Cash, 0)));

        // Caissier peut cloturer sa caisse (5000 + 4000 = 9000 attendu)
        var closed = await _cash.CloseAsync(new CloseCashSessionDto(session.Id, 9000m, 0, "RAS"));
        Assert.Equal(9000m, closed.ExpectedAmount);
        Assert.Equal(0m, closed.Discrepancy);
    }

    [Fact]
    public async Task Admin_CanManageExpensesCancelAndRefundWithAuditTrailInSqlite()
    {
        var admin = await _auth.SeedAdminAndSignInAsync();
        var order1 = await SeedOrderAsync(8000m);
        var order2 = await SeedOrderAsync(6000m);

        var session = await _cash.OpenAsync(new OpenCashSessionDto(10000m, admin.Id));

        var pCash = await _payments.RegisterAsync(new RegisterPaymentDto(order1.Id, 5000m, PaymentMethod.Cash, admin.Id));
        var pMomo = await _payments.RegisterAsync(new RegisterPaymentDto(order1.Id, 3000m, PaymentMethod.OrangeMoney, admin.Id, "OM-SQL-99"));

        // Reference Mobile Money en doublon refusee meme sur une autre commande
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _payments.RegisterAsync(new RegisterPaymentDto(order2.Id, 2000m, PaymentMethod.OrangeMoney, admin.Id, " om-sql-99 ")));

        await _payments.CancelAsync(new CancelPaymentDto(pCash.Id, "Erreur encaissement", admin.Id));
        await _payments.RefundAsync(new CancelPaymentDto(pMomo.Id, "Client annule", admin.Id));

        Assert.Equal(8000m, await _payments.GetBalanceAsync(order1.Id));

        var exp1 = await _expenses.CreateAsync(new CreateExpenseDto(_auth.Time.GetUtcNow().UtcDateTime, "Electricite", 2500m, PaymentMethod.Cash, admin.Id, "Recharge"));
        var exp2 = await _expenses.CreateAsync(new CreateExpenseDto(_auth.Time.GetUtcNow().UtcDateTime, "Erreur", 1000m, PaymentMethod.Cash, admin.Id));
        await _expenses.CancelAsync(exp2.Id, "Saisie en double", admin.Id);

        // Expected = 10000 + 0 (pCash annule) - 2500 (exp2 annulee) = 7500
        var closed = await _cash.CloseAsync(new CloseCashSessionDto(session.Id, 7200m, admin.Id, "Manquant 300"));
        Assert.Equal(7500m, closed.ExpectedAmount);
        Assert.Equal(-300m, closed.Discrepancy);

        var foundExpenses = await _expenses.SearchAsync(
            _auth.Time.GetUtcNow().UtcDateTime.AddMinutes(-5),
            _auth.Time.GetUtcNow().UtcDateTime.AddMinutes(5),
            "electricite");
        Assert.Single(foundExpenses);
        Assert.Equal(exp1.Id, foundExpenses[0].Id);

        var logs = await _auth.Audit.GetRecentAsync(50);
        var actions = logs.Select(l => l.Action).ToList();
        Assert.Contains(AuditActions.PaymentModified, actions);
        Assert.Contains(AuditActions.PaymentRefunded, actions);
        Assert.Contains(AuditActions.CashSessionClosed, actions);
        Assert.Contains("ExpenseCancelled", actions);
    }
}
