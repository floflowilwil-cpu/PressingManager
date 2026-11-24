using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Entities;

namespace PressingManager.Tests.Integration.Support;

/// <summary>
/// Contexte minimal pour tester les services du module traitement / stocks sans dependre du
/// PressingDbContext partage. Le modele repose sur les conventions EF Core ; le schema
/// detaille (index, longueurs...) est verifie par ProcessingInventorySchemaTests.
/// </summary>
public sealed class ProcessingTestDbContext : DbContext
{
    public ProcessingTestDbContext(DbContextOptions<ProcessingTestDbContext> options) : base(options)
    {
    }

    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServicePrice> ServicePrices => Set<ServicePrice>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
}

/// <summary>Base SQLite en memoire, creee pour un test et detruite apres.</summary>
public sealed class ProcessingTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public ProcessingTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ProcessingTestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ProcessingTestDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
