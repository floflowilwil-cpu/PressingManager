using PressingManager.Core.Enums;

namespace PressingManager.Core.Entities;

/// <summary>
/// Un changement de statut d'une commande : qui, quand, de quel statut vers quel statut.
/// OrderId et ChangedByUserId sont de simples cles etrangeres : les entites Order
/// (module commandes) et User (module authentification) sont fournies par d'autres membres.
/// </summary>
public class OrderStatusHistory
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    /// <summary>Statut precedent ; null pour le tout premier statut de la commande.</summary>
    public OrderStatus? FromStatus { get; set; }

    public OrderStatus ToStatus { get; set; }

    public int ChangedByUserId { get; set; }

    /// <summary>Date du changement, en UTC.</summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>True si c'est une correction autorisee (retour en arriere).</summary>
    public bool IsCorrection { get; set; }

    public string? Comment { get; set; }
}
