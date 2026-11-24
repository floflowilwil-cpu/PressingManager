using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.App.Localization;
using PressingManager.App.ViewModels;
using PressingManager.App.Views;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure;
using PressingManager.Infrastructure.Data;
using PressingManager.Infrastructure.Data.Seed;

namespace PressingManager.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Langue memorisee (francais par defaut) : a charger avant d'afficher la premiere fenetre.
        Loc.Instance.LoadSaved();

        try
        {
            // Configuration : appsettings.json (modele commite) < appsettings.local.json (secrets, ignore par git)
            // < variables d'environnement (ex. ConnectionStrings__Pressing).
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.local.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string? configuredConnection = configuration.GetConnectionString("Pressing");
            bool hasPostgresConnection = !string.IsNullOrWhiteSpace(configuredConnection)
                && !configuredConnection.Contains("CHANGE_ME", StringComparison.Ordinal);

            string connectionString;
            if (hasPostgresConnection)
            {
                connectionString = configuredConnection!;
            }
            else
            {
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PressingManager");
                Directory.CreateDirectory(appDataPath);
                connectionString = $"Data Source={Path.Combine(appDataPath, "pressingmanager-local.db")}";
            }

            var services = new ServiceCollection();

            services.AddPressingInfrastructure(connectionString);
            services.AddTransient<LoginViewModel>();
            services.AddTransient<PaymentManagementViewModel>();
            services.AddTransient<PaymentManagementView>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<ReportsView>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<UserManagementView>();
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardWindow>();

            _serviceProvider = services.BuildServiceProvider();

            using (var db = _serviceProvider.GetRequiredService<IDbContextFactory<PressingDbContext>>().CreateDbContext())
            {
                if (hasPostgresConnection)
                {
                    db.Database.Migrate();
                }
                else
                {
                    db.Database.EnsureCreated();
                }

                // Comptes de depart de l'equipe (idempotent). Mettre SeedDemoUsers a false pour les desactiver.
                bool seedUsers = !bool.TryParse(configuration["SeedDemoUsers"], out var flag) || flag;
                if (seedUsers)
                {
                    UserSeed.Seed(db, _serviceProvider.GetRequiredService<IPasswordHasher>());
                }

#if DEBUG
                // Donnees d'exemple (services, tarifs, produits) pour tester ; tarifs a ajuster avec le gerant.
                ProcessingSeed.Seed(db);
#endif
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible de démarrer Pressing Manager :\n{ex.Message}",
                "Erreur de démarrage",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
