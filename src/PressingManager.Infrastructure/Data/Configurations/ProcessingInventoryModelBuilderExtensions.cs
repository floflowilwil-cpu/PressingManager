using Microsoft.EntityFrameworkCore;

namespace PressingManager.Infrastructure.Data.Configurations;

public static class ProcessingInventoryModelBuilderExtensions
{
    /// <summary>
    /// Applique les configurations du module traitement / services / tarifs / stocks.
    /// A appeler depuis OnModelCreating de PressingDbContext (ou laisser
    /// ApplyConfigurationsFromAssembly les decouvrir).
    /// DbSet a declarer dans PressingDbContext : Service, ServicePrice,
    /// OrderStatusHistory, Product, StockMovement.
    /// </summary>
    public static ModelBuilder ApplyProcessingInventoryConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ServiceConfiguration());
        modelBuilder.ApplyConfiguration(new ServicePriceConfiguration());
        modelBuilder.ApplyConfiguration(new OrderStatusHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new StockMovementConfiguration());
        return modelBuilder;
    }
}
