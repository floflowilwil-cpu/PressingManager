using Microsoft.Extensions.DependencyInjection;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

public static class ProcessingAndInventoryServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre les services du module traitement / services / tarifs / stocks.
    /// A appeler depuis AddPressingInfrastructure, apres l'enregistrement de IDbContextFactory,
    /// IAuditService et TimeProvider (comme les autres modules : services en singleton).
    /// </summary>
    public static IServiceCollection AddProcessingAndInventoryServices(this IServiceCollection services)
    {
        services.AddSingleton<IServiceCatalogService, ServiceCatalogService>();
        services.AddSingleton<IStockService, StockService>();
        services.AddSingleton<IOrderProcessingService, OrderProcessingService>();
        return services;
    }
}
