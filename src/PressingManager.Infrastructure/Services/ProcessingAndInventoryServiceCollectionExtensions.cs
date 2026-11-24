using Microsoft.Extensions.DependencyInjection;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

public static class ProcessingAndInventoryServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre les services du module traitement / services / tarifs / stocks.
    /// Prerequis : un DbContext doit etre enregistre (ex. services.AddScoped&lt;DbContext&gt;(sp =>
    /// sp.GetRequiredService&lt;PressingDbContext&gt;())).
    /// </summary>
    public static IServiceCollection AddProcessingAndInventoryServices(this IServiceCollection services)
    {
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IOrderProcessingService, OrderProcessingService>();
        return services;
    }
}
