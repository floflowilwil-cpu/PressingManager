# Module Traitement, services, tarifs et stocks (Personne 3)

Branche : `feature/processing-inventory`

## Contenu

| Couche | Fichiers |
|---|---|
| Core (contrats) | Entities (`Service`, `ServicePrice`, `Product`, `StockMovement`, `OrderStatusHistory`), Enums, DTOs, Interfaces, `OrderStatusRules`, `StockCalculator` |
| Infrastructure | `Data/Configurations` (5 configurations + `ProcessingInventoryRelationsConfiguration`), `Services/ServiceCatalogService`, `StockService`, `OrderProcessingService`, `Data/Seed/ProcessingSeed` |
| Tests | `Unit/` (regles pures) et `Integration/` (services sur SQLite en memoire avec le vrai `PressingDbContext`) |

## Branchement

Les trois services suivent le schema des autres modules : `IDbContextFactory<PressingDbContext>`, `TimeProvider`,
erreurs metier en `BusinessRuleException`, enregistrement en singleton. Dans `AddPressingInfrastructure` :

```csharp
// Module traitement, services, tarifs et stocks (Personne 3)
services.AddProcessingAndInventoryServices();
```

Donnees de depart facultatives (services et produits d'exemple, tarifs a ajuster) :
`ProcessingSeed.Seed(db);` apres `db.Database.Migrate();` dans `App.xaml.cs`.

## Regles implementees

- Tarif : un nouveau tarif cloture l'ancien (`ValidTo`) pour la meme unite ; l'historique est conserve. Meme montant = aucun changement. Chaque changement est ecrit dans `AuditLogs` (action `ServicePriceChanged`).
- Stock : jamais stocke, recalcule depuis les mouvements. Une sortie ou un ajustement a la baisse qui rendrait le stock negatif est refuse.
- Statuts : le statut de reference est `Order.Status`. Un changement valide met a jour la commande et ajoute un evenement dans `OrderStatusHistory` en une seule sauvegarde. Les transitions sont controlees par `OrderStatusRules` ; un retour en arriere autorise est marque `IsCorrection`.
- Cles etrangeres : `OrderStatusHistory.OrderId` vers `Orders`, `OrderStatusHistory.ChangedByUserId` et `StockMovement.UserId` vers `Users`.

## Limites connues

- Controle des permissions : pas encore applique dans ces trois services (a faire avec `ICurrentUserContext.Require`).
- L'historique de statut commence au premier changement ; il n'est pas reconstitue pour les commandes creees avant.
- `OrderItem.ServiceId` n'a pas de cle etrangere vers `Services` (a convenir avec la personne 2).
