using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Data;
using PressingManager.Infrastructure.Services;

namespace PressingManager.Tests.Unit;

/// <summary>Base SQLite en memoire (une connexion ouverte = une base pour toute la duree du test).</summary>
public sealed class TestDb : IDbContextFactory<PressingDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<PressingDbContext> _options;

    public TestDb()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<PressingDbContext>().UseSqlite(_connection).Options;

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public PressingDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>Horloge controlable pour tester le verrouillage de compte.</summary>
public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public TestTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan duration) => _now += duration;
}

/// <summary>Assemble les vrais services sur une base en memoire.</summary>
public sealed class AuthTestContext : IDisposable
{
    public const string AdminPassword = "Secret123";

    public AuthTestContext()
    {
        var hasher = new Pbkdf2PasswordHasher(iterations: 1_000);

        Audit = new AuditService(Db, Session, Time);
        Auth = new AuthService(Db, hasher, Session, Audit, Time);
        Users = new UserService(Db, hasher, Session, Audit, Time);
    }

    public TestDb Db { get; } = new();

    public TestTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero));

    public CurrentUserContext Session { get; } = new();

    public AuditService Audit { get; }

    public AuthService Auth { get; }

    public UserService Users { get; }

    /// <summary>Cree le premier administrateur ("admin") et ouvre sa session.</summary>
    public async Task<UserDto> SeedAdminAndSignInAsync()
    {
        var admin = await Auth.CreateInitialAdministratorAsync("Admin Test", "admin", AdminPassword);
        Session.SignIn(admin);
        return admin;
    }

    public void SignInAs(UserRole role) =>
        Session.SignIn(new UserDto(999, "Utilisateur Test", "test", role, true, null));

    public void Dispose() => Db.Dispose();
}
