using PressingManager.Core.Enums;

namespace PressingManager.Core.Entities;

/// <summary>
/// Depense du pressing (achat de detergent, electricite, entretien...).
/// Une depense n'est jamais supprimee physiquement : elle peut etre annulee avec motif.
/// </summary>
public class Expense
{
    public int Id { get; set; }

    /// <summary>Date de la depense, en UTC.</summary>
    public DateTime Date { get; set; }

    public string Category { get; set; } = string.Empty;

    /// <summary>Montant de la depense en decimal (strictement positif).</summary>
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public string? Comment { get; set; }

    public int CreatedByUserId { get; set; }

    /// <summary>Session de caisse associee lorsque Method == Cash.</summary>
    public int? CashSessionId { get; set; }

    public CashSession? CashSession { get; set; }

    public bool IsCancelled { get; set; }

    public string? CancelReason { get; set; }

    public int? CancelledByUserId { get; set; }

    /// <summary>Date et heure d'annulation, en UTC.</summary>
    public DateTime? CancelledAt { get; set; }
}
