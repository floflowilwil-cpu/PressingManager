using Microsoft.EntityFrameworkCore;
using PressingManager.Infrastructure.Data.Seed;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

public class ProcessingSeedTests : IDisposable
{
    private readonly ProcessingTestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_CreatesServicesWithPricesAndProducts()
    {
        using var db = _db.CreateDbContext();

        ProcessingSeed.Seed(db);

        var services = db.Services.Include(s => s.Prices).ToList();
        Assert.Equal(3, services.Count);
        Assert.All(services, s => Assert.Single(s.Prices));
        Assert.Equal(4, db.Products.Count());
    }

    [Fact]
    public void Seed_CalledTwice_DoesNotDuplicateAnything()
    {
        using var db = _db.CreateDbContext();

        ProcessingSeed.Seed(db);
        ProcessingSeed.Seed(db);

        Assert.Equal(3, db.Services.Count());
        Assert.Equal(3, db.ServicePrices.Count());
        Assert.Equal(4, db.Products.Count());
    }
}
