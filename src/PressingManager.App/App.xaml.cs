
using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.App.ViewModels;
using PressingManager.App.Views;
using PressingManager.Infrastructure;
using PressingManager.Infrastructure.Data;
using PressingManager.Infrastructure.Data.Seed;
using PressingManager.Infrastructure.Services;

namespace PressingManager.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PressingManager");

            Directory.CreateDirectory(appDataPath);

            string databasePath = Path.Combine(
                appDataPath,
                "pressingmanager.db");

            var services = new ServiceCollection();

            services.AddPressingInfrastructure(databasePath);

            // Module traitement / services / tarifs / stocks (Personne 3) : ses services attendent un
            // DbContext "simple", fourni ici depuis la meme fabrique que les autres modules.
            services.AddScoped<DbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<PressingDbContext>>().CreateDbContext());
            services.AddProcessingAndInventoryServices();

            // Onglets Clients et Commandes
            services.AddTransient<CustomersViewModel>();
            services.AddTransient<CustomersView>();
            services.AddTransient<OrdersViewModel>();
            services.AddTransient<OrdersView>();
            services.AddTransient<NewOrderViewModel>();
            services.AddTransient<Func<NewOrderViewModel>>(sp => () => sp.GetRequiredService<NewOrderViewModel>());
            services.AddTransient<LoginViewModel>();
            services.AddTransient<PaymentManagementViewModel>();
            services.AddTransient<PaymentManagementView>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<ReportsView>();
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardWindow>();

            _serviceProvider = services.BuildServiceProvider();

            using (var db = _serviceProvider.GetRequiredService<IDbContextFactory<PressingDbContext>>().CreateDbContext())
            {
                db.Database.Migrate();

                // Services (lavage, repassage...) et produits de depart, sans effet s'ils existent deja :
                // sans eux, la liste des services de « Nouvelle commande » serait vide.
                ProcessingSeed.SeedAsync(db).GetAwaiter().GetResult();
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
