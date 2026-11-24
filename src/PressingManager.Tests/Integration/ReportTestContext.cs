using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Unit;

namespace PressingManager.Tests.Integration;

/// <summary>Base SQLite en mémoire avec un client, et des méthodes pour créer commandes, paiements et dépenses.</summary>
public sealed class ReportTestContext : IDisposable
{
    public ReportTestContext()
    {
        using var db = Db.CreateDbContext();
        var customer = new Customer { CustomerNumber = "C-001", FullName = "Awa Nkodo", Phone = "670000000" };
        db.Customers.Add(customer);

        // Vrai utilisateur en base : le journal d'audit a une clé étrangère vers Users.
        var user = new User
        {
            FullName = "Utilisateur Test",
            Username = "test",
            PasswordHash = "non-utilise",
            Role = UserRole.Administrator,
            IsActive = true,
            CreatedAt = Utc(1, 0)
        };
        db.Users.Add(user);
        db.SaveChanges();

        CustomerId = customer.Id;
        _userId = user.Id;

        SignInAs(UserRole.Administrator);
    }

    private readonly int _userId;

    public TestDb Db { get; } = new();

    public CurrentUserContext Session { get; } = new();

    public int CustomerId { get; }

    public static DateTime Utc(int day, int hour = 9, int month = 11) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    public void SignInAs(UserRole role) =>
        Session.SignIn(new UserDto(_userId, "Utilisateur Test", "test", role, true, null));

    public async Task<int> AddOrderAsync(string number, DateTime createdAtUtc, OrderStatus status, decimal total)
    {
        await using var db = Db.CreateDbContext();

        var order = new Order
        {
            OrderNumber = number,
            CustomerId = CustomerId,
            CreatedAt = createdAtUtc,
            Status = status,
            Items = { new OrderItem { ServiceId = 1, ItemType = "Chemise", Quantity = 1, UnitPrice = total } }
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    public async Task AddPaymentAsync(
        int orderId, decimal amount, DateTime paidAtUtc, PaymentStatus status = PaymentStatus.Valid)
    {
        await using var db = Db.CreateDbContext();

        db.Payments.Add(new Payment
        {
            OrderId = orderId,
            Amount = amount,
            PaidAt = paidAtUtc,
            Method = PaymentMethod.Cash,
            Status = status,
            ReceivedByUserId = _userId
        });
        await db.SaveChangesAsync();
    }

    public async Task AddExpenseAsync(decimal amount, DateTime dateUtc, bool cancelled = false)
    {
        await using var db = Db.CreateDbContext();

        db.Expenses.Add(new Expense
        {
            Date = dateUtc,
            Category = "Produits",
            Amount = amount,
            Method = PaymentMethod.Cash,
            CreatedByUserId = _userId,
            IsCancelled = cancelled
        });
        await db.SaveChangesAsync();
    }

    public void Dispose() => Db.Dispose();
}
