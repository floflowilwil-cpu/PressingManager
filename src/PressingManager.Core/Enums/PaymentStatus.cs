namespace PressingManager.Core.Enums;

/// <summary>
/// Statut d'un paiement. Un paiement n'est jamais supprime physiquement de la base :
/// il passe a Cancelled ou Refunded pour conserver la tracabilite.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Paiement valide et pris en compte dans le calcul du solde.</summary>
    Valid = 0,

    /// <summary>Paiement annule (avec motif et tracabilite).</summary>
    Cancelled = 1,

    /// <summary>Paiement rembourse au client (avec motif et tracabilite).</summary>
    Refunded = 2
}
