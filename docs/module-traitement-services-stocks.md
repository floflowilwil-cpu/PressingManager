# Module Traitement, services, tarifs et stocks (Personne 3)

Branche : `feature/processing-inventory`

## Contenu

| Couche | Fichiers |
|---|---|
| Core (contrats) | Entities (`Service`, `ServicePrice`, `Product`, `StockMovement`, `OrderStatusHistory`), Enums, DTOs, Interfaces, `OrderStatusRules`, `StockCalculator` |
| Infrastructure | `Services/ServiceCatalogService`, `StockService`, `OrderProcessingService`, `Data/Seed/ProcessingSeed` |
| Tests | `Unit/` (regles pures) et `Integration/` (services sur SQLite en memoire) |

## Brancher le module dans le PressingDbContext partage

```csharp
public DbSet<Service> Services => Set<Service>();
public DbSet<ServicePrice> ServicePrices => Set<ServicePrice>();
public DbSet<Product> Products => Set<Product>();
public DbSet<StockMovement> StockMovements => Set<StockMovement>();
public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Reutiliser la methode de ProcessingInventoryModelBuilderExtensions
// (ou ApplyConfigurationsFromAssembly si les configurations sont des IEntityTypeConfiguration).
}
```

Enregistrement des services (App) :

```csharp
services.AddDbContext<PressingDbContext>(...);
services.AddScoped<DbContext>(sp => sp.GetRequiredService<PressingDbContext>());
services.AddProcessingAndInventoryServices();
```

## Regles implementees

- Tarif : un nouveau tarif cloture l'ancien (`ValidTo`) pour la meme unite ; l'historique est conserve. Meme montant = aucun changement.
- Stock : jamais stocke, recalcule depuis les mouvements. Une sortie ou un ajustement a la baisse qui rendrait le stock negatif est refuse.
- Statuts : transitions controlees par `OrderStatusRules`. Une correction (retour en arriere autorise) est marquee `IsCorrection` dans l'historique. Sans historique, une commande est consideree `Received`.

## Points a traiter a l'integration

- Module commandes : creer l'historique initial a la creation de la commande, verifier l'existence de la commande et recopier le statut sur `Order`.
- Module securite : cles etrangeres vers `Users`, controle des permissions, trace des changements de tarif dans `AuditLogs`.
