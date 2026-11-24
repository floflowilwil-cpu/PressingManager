using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Tests.Integration.Support;

/// <summary>
/// Base SQLite en memoire avec le vrai PressingDbContext, creee pour un test et detruite apres.
/// Joue aussi le role d'IDbContextFactory, comme dans l'application.
/// </summary>
public sealed class ProcessingTestDb : IDbContextFactory<PressingDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private int _sequence;

    public ProcessingTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateDbContext();
        context.Database.EnsureCreated();
    }

    public PressingDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PressingDbContext>().UseSqlite(_connection).Options);

    /// <summary>Insere un utilisateur et renvoie son identifiant.</summary>
    public int AddUser(bool isActive = true)
    {
        using var db = CreateDbContext();

        var user = new User
        {
            FullName = "Agent test",
            Username = $"agent{++_sequence}",
            PasswordHash = "hash",
            IsActive = isActive
        };

        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    /// <summary>Insere un client et une commande, et renvoie l'identifiant de la commande.</summary>
    public int AddOrder(OrderStatus status = OrderStatus.Received)
    {
        using var db = CreateDbContext();
        var number = ++_sequence;

        var customer = new Customer
        {
            CustomerNumber = $"CLI-{number:0000}",
            FullName = "Client test",
            Phone = "690000000"
        };

        var order = new Order
        {
            OrderNumber = $"CMD-TEST-{number:0000}",
            Customer = customer,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        db.Orders.Add(order);
        db.SaveChanges();
        return order.Id;
    }

    public void Dispose() => _connection.Dispose();
}
