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
| Rapports | `IReportService` + `ReportCalculator` + `CsvExporter` | calculs et export prêts ; implémentation EF à ajouter (voir plus bas) |

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

## Reste à faire (dépend des autres modules)

- `ReportService` (implémentation de `IReportService`) et écran Rapports : à écrire quand `Order`, `Payment` et `Expense` (Personnes 2 et 4) sont fusionnés dans `develop`.
  Il suffira de lire les commandes de la période, de remplir des `OrderReportRow`, puis d'appeler `ReportCalculator` et `CsvExporter`.
- Écran « Journal d'audit » (lecture seule, `IAuditService.GetRecentAsync`).
- Écran « Changer mon mot de passe » (`IUserService.ChangeOwnPasswordAsync` est prêt).
- Export PDF (phase dédiée, section 4.10 de la documentation) : l'export CSV s'ouvre directement dans Excel.
