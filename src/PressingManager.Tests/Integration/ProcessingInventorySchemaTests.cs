using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Data.Configurations;

namespace PressingManager.Tests.Integration;

/// <summary>
/// Verifie que les configurations EF Core du module creent un schema SQLite valide
/// et que les donnees font l'aller-retour. Utilise un DbContext de test local, en attendant
/// le PressingDbContext partage.
/// </summary>
public class ProcessingInventorySchemaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TestDbContext> _options;

    public ProcessingInventorySchemaTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection).Options;

        using var context = new TestDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Service_WithPrices_RoundTrips()
    {
        using (var context = new TestDbContext(_options))
        {
            var service = new Service { Name = "Repassage" };
            service.Prices.Add(new ServicePrice
            {
                Unit = BillingUnit.Piece,
                Amount = 1500.50m,
                ValidFrom = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            context.Set<Service>().Add(service);
            context.SaveChanges();
        }

        using (var context = new TestDbContext(_options))
        {
            var service = context.Set<Service>().Include(s => s.Prices).Single();
            var price = Assert.Single(service.Prices);
            Assert.Equal(1500.50m, price.Amount);
            Assert.Equal(BillingUnit.Piece, price.Unit);
            Assert.Null(price.ValidTo);
        }
    }

    [Fact]
    public void Service_DuplicateName_IsRejected()
    {
        using var context = new TestDbContext(_options);
        context.Set<Service>().Add(new Service { Name = "Lavage" });
        context.Set<Service>().Add(new Service { Name = "Lavage" });

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Product_WithMovements_CurrentStockIsComputedFromMovements()
    {
        using (var context = new TestDbContext(_options))
        {
            var product = new Product { Name = "Detergent", Unit = "kg", AlertThreshold = 2.5m };
            var date = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
            product.StockMovements.Add(new StockMovement { Type = StockMovementType.Entry, Quantity = 10m, MovementDate = date, UserId = 1 });
            product.StockMovements.Add(new StockMovement { Type = StockMovementType.Exit, Quantity = 2.5m, MovementDate = date, UserId = 1 });
            context.Set<Product>().Add(product);
            context.SaveChanges();
        }

        using (var context = new TestDbContext(_options))
        {
            var product = context.Set<Product>().Include(p => p.StockMovements).Single();
            Assert.Equal(2.5m, product.AlertThreshold);
            Assert.Equal(7.5m, StockCalculator.GetCurrentStock(product.StockMovements));
        }
    }

    [Fact]
    public void OrderStatusHistory_RoundTrips_WithNullableFromStatus()
    {
        using (var context = new TestDbContext(_options))
        {
            context.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                OrderId = 42,
                FromStatus = null,
                ToStatus = OrderStatus.Received,
                ChangedByUserId = 1,
                ChangedAt = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)
            });
            context.SaveChanges();
        }

        using (var context = new TestDbContext(_options))
        {
            var history = context.Set<OrderStatusHistory>().Single();
            Assert.Equal(42, history.OrderId);
            Assert.Null(history.FromStatus);
            Assert.Equal(OrderStatus.Received, history.ToStatus);
        }
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyProcessingInventoryConfigurations();
        }
    }
}
