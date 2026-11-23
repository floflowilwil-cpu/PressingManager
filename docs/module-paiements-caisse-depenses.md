# Module Paiements, Caisse et Dépenses (Personne 4)

Branche : `feature/payments-expenses`

Ce module gère les encaissements clients, les sessions de caisse journalières et le suivi des dépenses du pressing, intégré à **SQLite / EF Core (`PressingDbContext`)**, au contrôle de permissions (**`ICurrentUserContext`**) et au journal d'audit (**`IAuditService`**).

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
  - `PaymentReceiptGenerator` : numérotation officielle des reçus (`REC-YYYYMMDD-0001`), libellés français des modes/statuts et formatage texte des reçus.
- **DTOs & Interfaces** (`src/PressingManager.Core/DTOs/` et `Interfaces/`) :
  - `IPaymentService`, `IExpenseService`, `ICashService`, `PaymentReceiptDto`
  - `IPaymentRepository`, `IExpenseRepository`, `ICashSessionRepository`
  - `IOrderBillingInfoProvider` (contrat de lecture des commandes)

### `PressingManager.App` (Vues WPF stylées & ViewModel)
- **ViewModel** : `ViewModels/PaymentManagementViewModel.cs`
- **Vues** :
  - `Views/PaymentManagementView.xaml` : cartes visuelles des moyens de paiement (Espèces, MTN Mobile Money, Orange Money, Autre), statut de caisse, bandeau de confirmation et historique.
  - `Views/PaymentReceiptWindow.xaml` : illustration vectorielle de validation réussie, aperçu du reçu officiel avec code-barres vectoriel, impression / PDF Windows (`PrintDialog`) et export `.txt`.

### `PressingManager.Infrastructure` (EF Core, Services et Injection)
- **Base de données & Configurations** (`Data/`) :
  - `PressingDbContext` : ajout des `DbSet` `Payments`, `Expenses`, `CashSessions` (ainsi que `Customers`, `Orders`, `OrderItems` pour le calcul des soldes).
  - `Data/Configurations/` : `PaymentConfiguration`, `ExpenseConfiguration`, `CashSessionConfiguration`.
- **Dépôts EF Core & en mémoire** (`Repositories/`) :
  - `EfPaymentRepository`, `EfExpenseRepository`, `EfCashSessionRepository`, `EfOrderBillingInfoProvider` (compatibles SQLite sans erreur `.Sum()` sur `decimal`).
  - `InMemoryPaymentRepository`, `InMemoryExpenseRepository`, `InMemoryCashSessionRepository` (tests unitaires rapides sans BDD).
- **Services** (`Services/`) :
  - `PaymentService`, `ExpenseService`, `CashService` enregistrés dans `DependencyInjection.AddPressingInfrastructure(...)`.

## 2. Règles métier, permissions et audit

1. **Précision monétaire** : tous les montants utilisent exclusivement le type `decimal`.
2. **Pas de suppression physique** : tout paiement annulé/remboursé ou dépense annulée reste conservé en base avec motif, auteur et horodatage UTC.
3. **Unicité Mobile Money** : les modes `MtnMobileMoney` et `OrangeMoney` exigent une référence non vide et unique parmi les paiements valides.
4. **Contrôle de caisse** : tout encaissement ou dépense en `Cash` est refusé si aucune session de caisse n'est ouverte (`CashSessionStatus.Open`).
5. **Écart à la clôture** : `ExpectedAmount = OpeningAmount + Paiements Cash valides - Dépenses Cash actives`, et `Discrepancy = CountedAmount - ExpectedAmount`.
6. **Permissions vérifiées côté service (`ICurrentUserContext`)** :
   - Encaissement et ouverture de caisse : `Permission.RegisterPayments`
   - Clôture de caisse : `Permission.CloseCashSession`
   - Annulation et remboursement d'un paiement : `Permission.RefundPayments`
   - Création et annulation d'une dépense : `Permission.ManageExpenses`
7. **Journal d'audit (`IAuditService`)** :
   - Annulation de paiement → `AuditActions.PaymentModified`
   - Remboursement de paiement → `AuditActions.PaymentRefunded`
   - Clôture de caisse → `AuditActions.CashSessionClosed`
   - Annulation de dépense → `"ExpenseCancelled"`

## 3. Compilation et tests

Depuis la racine du dépôt :

```powershell
dotnet build
dotnet test --verbosity normal
```

- `Unit/PaymentRulesTests.cs` & `Unit/CashCalculatorTests.cs` : règles pures.
- `Unit/PaymentAndCashServicesTests.cs` : tests de bout en bout des services en mémoire.
- `Integration/PaymentAndCashIntegrationTests.cs` : tests réels sur SQLite en mémoire (`TestDb`) avec permissions et audit.
