
using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.App.Services;
using PressingManager.App.ViewModels;
using PressingManager.App.Views;
using PressingManager.Infrastructure;
using PressingManager.Infrastructure.Data;

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

            // Service de langue partagé dans toute l'application
            services.AddSingleton<LanguageService>();

            services.AddTransient<LoginViewModel>();
            services.AddTransient<PaymentManagementViewModel>();
            services.AddTransient<PaymentManagementView>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<ReportsView>();
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardWindow>();

            _serviceProvider = services.BuildServiceProvider();

            using (var db = _serviceProvider
                .GetRequiredService<IDbContextFactory<PressingDbContext>>()
                .CreateDbContext())
            {
                db.Database.Migrate();
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
