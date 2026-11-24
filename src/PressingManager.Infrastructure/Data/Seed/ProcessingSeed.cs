using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;

namespace PressingManager.Infrastructure.Data.Seed;

/// <summary>
/// Donnees de depart du module traitement / stocks (catalogue et produits).
/// Les tarifs sont des EXEMPLES a ajuster avec le gerant. Idempotent : ne cree les services
/// (ou les produits) que si la table correspondante est vide.
/// </summary>
public static class ProcessingSeed
{
    public static void Seed(DbContext db, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        if (!db.Set<Service>().Any())
        {
            db.Set<Service>().AddRange(
                NewService("Lavage", "Lavage et séchage du linge", BillingUnit.Kilogram, 500m, now),
                NewService("Repassage", "Repassage et finition", BillingUnit.Piece, 200m, now),
                NewService("Nettoyage à sec", "Nettoyage à sec des vêtements délicats", BillingUnit.Piece, 1500m, now));
        }

        if (!db.Set<Product>().Any())
        {
            db.Set<Product>().AddRange(
                new Product { Name = "Détergent", Unit = "kg", AlertThreshold = 5m },
                new Product { Name = "Assouplissant", Unit = "L", AlertThreshold = 5m },
                new Product { Name = "Cintres", Unit = "unité", AlertThreshold = 50m },
                new Product { Name = "Sachets de livraison", Unit = "unité", AlertThreshold = 100m });
        }

        db.SaveChanges();
    }

    private static Service NewService(string name, string description, BillingUnit unit, decimal amount, DateTime validFrom)
    {
        var service = new Service { Name = name, Description = description, IsActive = true };
        service.Prices.Add(new ServicePrice { Unit = unit, Amount = amount, ValidFrom = validFrom });
        return service;
    }
}
