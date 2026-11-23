
using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.App.ViewModels;
using PressingManager.Infrastructure;

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
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

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
