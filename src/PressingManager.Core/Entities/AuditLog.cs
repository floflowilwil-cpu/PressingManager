namespace PressingManager.Core.Entities;

/// <summary>Trace d'une operation sensible : qui, quoi, sur quoi, quand.</summary>
public class AuditLog
{
    public int Id { get; set; }

    /// <summary>Auteur ; null si aucun utilisateur connecte (ex. echec de connexion).</summary>
    public int? UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Voir <see cref="Common.AuditActions"/>.</summary>
    public string Action { get; set; } = string.Empty;

    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public string? Details { get; set; }

    /// <summary>Date de l'operation, en UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
