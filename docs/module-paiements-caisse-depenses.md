# Module Paiements, Caisse et Dépenses (Personne 4)

Ce module gère les encaissements clients, les sessions de caisse journalières et le suivi des dépenses du pressing.

## 1. Périmètre et architecture

### `PressingManager.Core` (Contrats et règles métier pures)
- **Enums** (`src/PressingManager.Core/Enums/`) :
  - `PaymentMethod` : `Cash`, `MtnMobileMoney`, `OrangeMoney`, `Other`
  - `PaymentStatus` : `Valid`, `Cancelled`, `Refunded`
  - `CashSessionStatus` : `Open`, `Closed`
- **Entités** (`src/PressingManager.Core/Entities/`) :
  - `Payment` : montant en `decimal`, lien `OrderId`, `CashSessionId`, `Reference` Mobile Money et champs de traçabilité d'annulation/remboursement.
  - `Expense` : catégorie, montant en `decimal`, mode de paiement, lien `CashSessionId`, annulation logique (`IsCancelled`, `CancelReason`, `CancelledByUserId`, `CancelledAt`).
  - `CashSession` : ouverture (`OpeningAmount`), clôture (`CountedAmount`, `ExpectedAmount`, `Discrepancy`).
- **Règles métier pures** (`src/PressingManager.Core/Common/`) :
  - `PaymentRules` : calcul du solde restant, validation des montants, blocage du trop-perçu, référence unique Mobile Money, contrôle d'ouverture de caisse et motif d'annulation/remboursement obligatoire.
  - `CashCalculator` : calcul du montant théorique en espèces et de l'écart à la clôture.
- **DTOs & Interfaces** (`src/PressingManager.Core/DTOs/` et `Interfaces/`) :
  - `IPaymentService`, `IExpenseService`, `ICashService`
  - `IPaymentRepository`, `IExpenseRepository`, `ICashSessionRepository`
  - `IOrderBillingInfoProvider` (contrat d'intégration avec le module Commandes)

### `PressingManager.Infrastructure` (Implémentation des services)
- **Services** (`src/PressingManager.Infrastructure/Services/`) :
  - `PaymentService` : encaissement, historique par commande, calcul du solde, annulation et remboursement sans suppression physique.
  - `ExpenseService` : création, recherche filtrée par date/catégorie et annulation tracée.
  - `CashService` : ouverture (une seule caisse ouverte à la fois), consultation de la session active et clôture avec calcul automatique de l'écart.
- **Repositories en mémoire** (`src/PressingManager.Infrastructure/Repositories/`) :
  - `InMemoryPaymentRepository`, `InMemoryExpenseRepository`, `InMemoryCashSessionRepository` (prêts pour les tests et en attente du `PressingDbContext` EF Core / SQLite).

## 2. Règles métier garanties

1. **Précision monétaire** : tous les montants utilisent exclusivement le type `decimal`.
2. **Pas de suppression physique** : tout paiement annulé/remboursé ou dépense annulée reste conservé en base avec motif, auteur et horodatage UTC.
3. **Unicité Mobile Money** : les modes `MtnMobileMoney` et `OrangeMoney` exigent une référence non vide et unique parmi les paiements valides.
4. **Contrôle de caisse** : tout encaissement ou dépense en `Cash` est refusé si aucune session de caisse n'est ouverte (`CashSessionStatus.Open`).
5. **Écart à la clôture** : `ExpectedAmount = OpeningAmount + Paiements Cash valides - Dépenses Cash actives`, et `Discrepancy = CountedAmount - ExpectedAmount`.

## 3. Points d'intégration avec l'équipe

- **Responsable d'intégration (EF Core / `PressingDbContext`)** :
  - Ajouter `DbSet<Payment>`, `DbSet<Expense>`, `DbSet<CashSession>` et leurs configurations `IEntityTypeConfiguration<T>`.
  - Remplacer ou compléter les dépôts `InMemory*` par des implémentations EF Core (en chargeant les montants avant `.Sum()` côté C# sous SQLite).
- **Personne 2 (Commandes)** : fournir une implémentation de `IOrderBillingInfoProvider` retournant `OrderBillingInfoDto(OrderId, TotalAmount, IsCancelled)`.
- **Personne 5 (Utilisateurs, Rôles et Audit)** : brancher la vérification de rôle (`Admin`/`Gérant` pour annulation/remboursement) et la journalisation dans `AuditLogs`.

## 4. Compilation et tests

Depuis la racine du dépôt :

```powershell
dotnet build
dotnet test --verbosity normal
```
