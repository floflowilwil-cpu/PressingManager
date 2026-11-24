using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;
using PressingManager.Infrastructure.Repositories;
using PressingManager.Infrastructure.Services;

namespace PressingManager.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Enregistre la base PostgreSQL (partagee entre tous les postes) ou SQLite locale en secours,
    /// ainsi que les services de l'infrastructure.
    /// Un IDbContextFactory est utilise (un contexte court par operation), ce qui convient a WPF.
    /// </summary>
    public static IServiceCollection AddPressingInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        bool useSqlite = connectionString.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);

        if (useSqlite)
        {
            services.AddDbContextFactory<PressingDbContext>(options =>
                options.UseSqlite(connectionString)
                       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        }
        else
        {
            NpgsqlSetup.Configure();
            services.AddDbContextFactory<PressingDbContext>(options =>
                options.UseNpgsql(connectionString)
                       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
        }

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher>(_ => new Pbkdf2PasswordHasher());
        services.AddSingleton<ICurrentUserContext, CurrentUserContext>();

        // Module authentification (Personne 5)
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IReportExportService, ReportExportService>();
        services.AddSingleton<IDashboardService, DashboardService>();

        // Module clients et commandes (Personne 2)
        services.AddSingleton<ICustomerService, CustomerService>();
        services.AddSingleton<IOrderService, OrderService>();

        // Module paiements, caisse et depenses (Personne 4)
        services.AddSingleton<IOrderBillingInfoProvider, EfOrderBillingInfoProvider>();
        services.AddSingleton<IPaymentService, PaymentService>();
        services.AddSingleton<IExpenseService, ExpenseService>();
        services.AddSingleton<ICashService, CashService>();

        // Module traitement, services, tarifs et stocks (Personne 3)
        services.AddProcessingAndInventoryServices();

        return services;
    }
}
