
using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.App.Localization;
using PressingManager.App.Services;
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

        // Charger la langue mémorisée avant l'affichage de la première fenêtre.
        Loc.Instance.LoadSaved();

        try
        {
            // Configuration : appsettings.json < appsettings.local.json
            // < variables d'environnement.
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.local.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string? configuredConnection =
                configuration.GetConnectionString("Pressing");

            bool hasPostgresConnection =
                !string.IsNullOrWhiteSpace(configuredConnection)
                && !configuredConnection.Contains(
                    "CHANGE_ME",
                    StringComparison.Ordinal);

            string connectionString;

            if (hasPostgresConnection)
            {
                connectionString = configuredConnection!;
            }
            else
            {
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "PressingManager");

                Directory.CreateDirectory(appDataPath);

                connectionString =
                    $"Data Source={Path.Combine(appDataPath, "pressingmanager-local.db")}";
            }

            var services = new ServiceCollection();

            services.AddPressingInfrastructure(connectionString);

            // Service de langue partagé dans toute l'application.
            services.AddSingleton<LanguageService>();

            services.AddTransient<LoginViewModel>();
            services.AddTransient<PaymentManagementViewModel>();
            services.AddTransient<PaymentManagementView>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<ReportsView>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<UserManagementView>();
            services.AddTransient<OrderProcessingViewModel>();
            services.AddTransient<OrderProcessingView>();
            services.AddTransient<ServiceCatalogViewModel>();
            services.AddTransient<ServiceCatalogView>();
            services.AddTransient<StockViewModel>();
            services.AddTransient<StockView>();
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardWindow>();

            _serviceProvider = services.BuildServiceProvider();

            using (var db = _serviceProvider
                .GetRequiredService<IDbContextFactory<PressingDbContext>>()
                .CreateDbContext())
            {
                if (hasPostgresConnection)
                {
                    db.Database.Migrate();
                }
                else
                {
                    db.Database.EnsureCreated();
                }

                // Création des comptes de départ.
                bool seedUsers =
                    !bool.TryParse(configuration["SeedDemoUsers"], out var flag)
                    || flag;

                if (seedUsers)
                {
                    UserSeed.Seed(
                        db,
                        _serviceProvider.GetRequiredService<IPasswordHasher>());
                }

#if DEBUG
                // Données de démonstration pour les traitements et les stocks.
                ProcessingSeed.Seed(db);
#endif
            }

            var mainWindow =
                _serviceProvider.GetRequiredService<MainWindow>();

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
