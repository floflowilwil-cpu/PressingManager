using PressingManager.Core.Enums;

namespace PressingManager.Core.Entities;

/// <summary>
/// Compte utilisateur. Un compte n'est jamais supprime : il est desactive,
/// afin de conserver l'historique (paiements, statuts, audit) qui le reference.
/// </summary>
public class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Identifiant de connexion, stocke en minuscules et unique.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Empreinte PBKDF2 du mot de passe (jamais le mot de passe en clair).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Date de creation, en UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Derniere connexion reussie, en UTC.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Echecs de connexion consecutifs depuis la derniere reussite.</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>Le compte est verrouille jusqu'a cette date (UTC) ; null = non verrouille.</summary>
    public DateTime? LockedUntil { get; set; }
}
