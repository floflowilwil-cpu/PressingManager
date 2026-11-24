# Module Authentification, utilisateurs, rôles, audit et rapports (Personne 5)

Branche : `feature/auth-reports`

## Ce que fournit le module

| Besoin | Contrat (Core) | Implémentation (Infrastructure) |
|---|---|---|
| Connexion / premier lancement | `IAuthService` | `AuthService` |
| Gestion des comptes | `IUserService` | `UserService` |
| Utilisateur connecté + permissions | `ICurrentUserContext` | `CurrentUserContext` |
| Journal d'audit | `IAuditService` | `AuditService` |
| Hachage des mots de passe | `IPasswordHasher` | `Pbkdf2PasswordHasher` (PBKDF2-SHA256, sans package externe) |
| Rapports | `IReportService` + `ReportCalculator` | `ReportService` (commandes, synthèse financière, soldes impayés, à récupérer) |
| Indicateurs du tableau de bord | `IDashboardService` + `DashboardCalculator` | `DashboardService` |
| Export CSV et PDF des rapports | `IReportExportService` + `CsvExporter` | `ReportExportService` + `PdfReportBuilder` (QuestPDF) ; permission + audit |

Écrans WPF : `Views/LoginWindow` et `Views/UserManagementView` (+ ViewModels).

## Utilisation par les autres modules

```csharp
// Dans un service métier (ex. PaymentService, Personne 4)
_session.Require(Permission.RefundPayments);          // lève PermissionDeniedException sinon
var userId = _session.CurrentUser!.Id;                // à stocker dans Payment.UserId, etc.
await _audit.LogAsync(AuditActions.PaymentRefunded, "Payment", payment.Id.ToString(), $"Motif : {reason}");
```

Les permissions de chaque rôle sont dans **un seul fichier** : `Core/Common/RolePermissions.cs`.
Les noms d'actions d'audit sont dans `Core/Common/AuditActions.cs`.

## Intégration (Personne 1)

1. Dans `App.xaml`, retirer `StartupUri`. Dans `App.xaml.cs` :

```csharp
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;   // la fenêtre de connexion ne doit pas fermer l'appli

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PressingManager");
        Directory.CreateDirectory(folder);

        var services = new ServiceCollection();
        services.AddPressingInfrastructure(Path.Combine(folder, "PressingManager.db"));
        services.AddTransient<LoginViewModel>();
        services.AddTransient<UserManagementViewModel>();
        Services = services.BuildServiceProvider();

        // Tant que les migrations EF Core ne sont pas créées :
        await using (var db = await Services.GetRequiredService<IDbContextFactory<PressingDbContext>>().CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        var login = new LoginWindow(Services.GetRequiredService<LoginViewModel>());
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }
}
```

2. Menu : afficher une entrée seulement si `session.HasPermission(...)` (confort visuel ; le vrai contrôle est dans les services).
   « Utilisateurs » → `Permission.ManageUsers`, « Rapports » → `Permission.ViewReports`.
3. Déconnexion : `await authService.LogoutAsync();` puis rouvrir `LoginWindow`.
4. `PressingDbContext` : chaque membre ajoute ses `DbSet` dans le même fichier et sa configuration dans `Data/Configurations/`
   (elles sont chargées automatiquement). Si un autre membre a déjà créé le fichier, fusionner les deux `DbSet` (`Users`, `AuditLogs`).

## Règles métier implémentées

- Premier lancement : aucun compte → l'écran de connexion demande de créer l'administrateur (aucun mot de passe par défaut dans le code ni dans Git).
- Mot de passe : 8 caractères minimum, au moins une lettre et un chiffre.
- Verrouillage 5 minutes après 5 échecs de connexion consécutifs.
- Un compte n'est jamais supprimé, seulement désactivé ; on ne peut pas désactiver son propre compte ni retirer le dernier administrateur actif.
- Le message d'échec de connexion ne dit pas si c'est l'identifiant ou le mot de passe qui est faux.
- Toutes les opérations de gestion de comptes et les connexions sont écrites dans `AuditLogs`.

## Tests

```
dotnet test
```

- `Unit/AuthRulesTests.cs` : permissions, politique de mot de passe, hachage.
- `Unit/ReportTests.cs` : calculs de rapports et export CSV.
- `Integration/AuthServiceTests.cs`, `Integration/UserServiceTests.cs` : services réels sur SQLite en mémoire.

## Pour la Personne 1 (écrans)

Tous ces services sont déjà enregistrés par `AddPressingInfrastructure`. Les écrans n'ont qu'à les appeler :

```csharp
// Période choisie à l'écran (dates locales, bornes incluses) -> intervalle UTC
var (fromUtc, toUtc) = ReportPeriod.ToUtcRange(fromDatePicker, toDatePicker, TimeZoneInfo.Local);

var orders      = await reports.GetOrdersAsync(fromUtc, toUtc);            // + statut optionnel
var summary     = await reports.GetFinancialSummaryAsync(fromUtc, toUtc);  // facturé, encaissé, dépenses, résultat net
var unpaid      = await reports.GetOutstandingBalancesAsync();
var toPickUp    = await reports.GetAwaitingPickupAsync();
var dashboard   = await dashboardService.GetSummaryAsync();                // tableau de bord

// Export : l'écran choisit le chemin (SaveFileDialog), le service vérifie la permission et écrit l'audit
await exportService.ExportOrdersAsync(orders, path, "Commandes 01/11/2026 - 30/11/2026");     // .csv
await exportService.ExportOrdersPdfAsync(orders, path, "Commandes 01/11/2026 - 30/11/2026");  // .pdf
```

- Les dates des `OrderReportRow` sont en **UTC** : les convertir en heure locale pour l'affichage (`ToLocalTime()`).
- Rapports et export : permissions `ViewReports` / `ExportReports` (administrateur uniquement dans la matrice actuelle).
  Tableau de bord : `ViewDashboard`. Les services lèvent `PermissionDeniedException` / `BusinessRuleException` : afficher `ex.Message`.
- Alertes de stock du tableau de bord : elles viennent de `IStockService` (Personne 3). Tant qu'il n'est pas enregistré,
  ou si l'utilisateur n'a pas `ViewStock`, `StockAlerts` est vide.
- Menu : afficher « Rapports » seulement si `session.HasPermission(Permission.ViewReports)`.

## Limites connues

- Les rapports chargent les commandes en mémoire pour calculer les montants (SQLite ne sait pas additionner des `decimal`
  côté base). Très suffisant pour un pressing ; à revoir si la base atteint des dizaines de milliers de commandes.
- « Encaissé » = paiements valides **datés dans la période** (même pour une commande créée avant) ; « Facturé » = commandes
  **créées dans la période**. Les deux ne se comparent donc pas ligne à ligne.
- Export PDF : liste des commandes uniquement (pas encore de PDF pour la synthèse financière). Il utilise QuestPDF (licence Community :
  gratuite pour un projet scolaire ou une petite entreprise). Le CSV s'ouvre directement dans Excel.
