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

Donnees de depart (services et produits d'exemple, tarifs a ajuster avec le gerant) :
`ProcessingSeed.Seed(db);` apres `db.Database.Migrate();` dans `App.xaml.cs`, uniquement en `DEBUG` (`#if DEBUG`) pour ne pas mettre de tarifs inventes dans une base reelle. En production, le gerant cree ses services depuis l'ecran Services et tarifs.

## Regles implementees

- Tarif : un nouveau tarif cloture l'ancien (`ValidTo`) pour la meme unite ; l'historique est conserve. Meme montant = aucun changement. Chaque changement est ecrit dans `AuditLogs` (action `ServicePriceChanged`).
- Stock : jamais stocke, recalcule depuis les mouvements. Une sortie ou un ajustement a la baisse qui rendrait le stock negatif est refuse.
- Statuts : le statut de reference est `Order.Status`. Un changement valide met a jour la commande et ajoute un evenement dans `OrderStatusHistory` en une seule sauvegarde. Les transitions sont controlees par `OrderStatusRules` ; un retour en arriere autorise est marque `IsCorrection`.
- Cles etrangeres : `OrderItem.ServiceId` vers `Services` (Restrict : un service utilise se desactive, il ne se supprime pas ; `OrderService.CreateAsync` refuse un service inconnu ou desactive avec un message clair), `OrderStatusHistory.OrderId` vers `Orders`, `OrderStatusHistory.ChangedByUserId` et `StockMovement.UserId` vers `Users`.

## Permissions (controlees dans les services, via `ICurrentUserContext.Require`)

| Service | Operation | Permission |
|---|---|---|
| `ServiceCatalogService` | creer, modifier, (de)sactiver un service, definir un tarif | `ManageServices` |
| `ServiceCatalogService` | lire services et tarifs | aucune (ouvert : la creation de commande en a besoin) |
| `StockService` | creer/modifier un produit, enregistrer un mouvement | `ManageStock` |
| `StockService` | lire produits, mouvements, alertes | `ViewStock` (le tableau de bord attrape le refus : liste d'alertes vide) |
| `OrderProcessingService` | changer un statut | `UpdateOrderStatus` |
| `OrderProcessingService` | correction (retour en arriere) | `UpdateOrderStatus` + `CorrectOrderStatus` |
| `OrderProcessingService` | historique, file de traitement, retards | `ViewOrders` |

Les services recoivent `ICurrentUserContext` par le constructeur (pas de valeur par defaut : un oubli de branchement ne desactive pas les controles).
Les annulations (`OrderCancelled`) et les corrections de statut (`OrderStatusCorrected`) sont ecrites dans `AuditLogs`.

## File de traitement

- `GetOrdersAsync()` : commandes encore a traiter (ni livrees ni annulees) ; `GetOrdersAsync(statut)` : un statut precis (ex. `ReadyForPickup`). Tri : date prevue la plus proche d'abord.
- `GetOverdueOrdersAsync()` : commandes dont le jour prevu est passe alors qu'elles ne sont pas encore pretes (`OrderStatusRules.IsOverdue`). Une commande prete a recuperer est signalee a part (`IsReadyForPickup`).

## Limites connues

- L'historique de statut commence au premier changement ; il n'est pas reconstitue pour les commandes creees avant.
