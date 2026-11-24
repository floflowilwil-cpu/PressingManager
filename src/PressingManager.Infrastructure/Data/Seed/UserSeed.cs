using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Data.Seed;

/// <summary>
/// Comptes de depart de l'equipe (base partagee). Idempotent : ne cree que les identifiants absents,
/// donc sans risque si plusieurs postes demarrent en meme temps ou relancent l'application.
/// ATTENTION : les mots de passe ci-dessous sont des mots de passe de DEMONSTRATION connus de toute l'equipe.
/// A desactiver (SeedDemoUsers = false dans appsettings) ou a changer avant une vraie mise en service.
/// </summary>
public static class UserSeed
{
    private sealed record SeedUser(string FullName, string Username, string Password, UserRole Role);

    // Identifiant = prenom en minuscules sans accent (UserRules n'accepte que lettres ASCII, chiffres, '.', '_' et '-').
    // Mot de passe = Prenom + 2026 (PasswordPolicy : au moins 8 caracteres, une lettre et un chiffre).
    // Deux comptes par role pour que chacun puisse tester son module : modifier ce tableau pour changer les roles.
    private static readonly SeedUser[] Users =
    [
        new("Florent", "florent", "Florent2026", UserRole.Administrator),
        new("Azra", "azra", "Azra2026", UserRole.Administrator),
        new("Iness", "iness", "Iness2026", UserRole.FrontDesk),
        new("John", "john", "John2026", UserRole.FrontDesk),
        new("Ange", "ange", "Ange2026", UserRole.Cashier),
        new("Rose", "rose", "Rose2026", UserRole.Cashier),
        new("Wilfried", "wilfried", "Wilfried2026", UserRole.Processor),
        new("Angel", "angel", "Angel2026", UserRole.Processor),
        new("Ruan", "ruan", "Ruan2026", UserRole.StockManager),
        new("Daphné", "daphne", "Daphne2026", UserRole.StockManager)
    ];

    public static void Seed(DbContext db, IPasswordHasher hasher, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hasher);

        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var existing = db.Set<User>().Select(u => u.Username).ToHashSet();

        // On ne hache (PBKDF2, volontairement lent) que les comptes qui manquent.
        var missing = Users.Where(u => !existing.Contains(u.Username)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var u in missing)
        {
            db.Set<User>().Add(new User
            {
                FullName = u.FullName,
                Username = u.Username,
                PasswordHash = hasher.Hash(u.Password),
                Role = u.Role,
                IsActive = true,
                CreatedAt = now
            });
        }

        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            // Un autre poste vient de creer les memes comptes (index unique sur Username) : rien a faire.
        }
    }
}
