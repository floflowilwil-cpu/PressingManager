using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;
using PressingManager.Infrastructure.Services;

namespace PressingManager.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Enregistre la base SQLite et les services de l'infrastructure.
    /// Un IDbContextFactory est utilise (un contexte court par operation), ce qui convient a WPF.
    /// Les autres membres ajoutent leurs services ici, a la suite.
    /// </summary>
    public static IServiceCollection AddPressingInfrastructure(this IServiceCollection services, string databasePath)
    {
        services.AddDbContextFactory<PressingDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher>(_ => new Pbkdf2PasswordHasher());
        services.AddSingleton<ICurrentUserContext, CurrentUserContext>();

        // Module authentification (Personne 5)
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IUserService, UserService>();

        return services;
    }
}
