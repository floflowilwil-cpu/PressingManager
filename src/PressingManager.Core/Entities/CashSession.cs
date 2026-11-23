using PressingManager.Core.Enums;

namespace PressingManager.Core.Entities;

/// <summary>
/// Session de caisse (ouverture avec fonds initial, suivi des especes, cloture avec calcul d'ecart).
/// </summary>
public class CashSession
{
    public int Id { get; set; }

    /// <summary>Date et heure d'ouverture de la caisse, en UTC.</summary>
    public DateTime OpenedAt { get; set; }

    /// <summary>Fonds de caisse initial en decimal.</summary>
    public decimal OpeningAmount { get; set; }

    public int OpenedByUserId { get; set; }

    /// <summary>Date et heure de cloture, en UTC.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Montant en especes reellement compte par le caissier a la cloture.</summary>
    public decimal? CountedAmount { get; set; }

    /// <summary>Montant theorique calcule (ouverture + encaissements especes - depenses especes).</summary>
    public decimal? ExpectedAmount { get; set; }

    /// <summary>Ecart de caisse (CountedAmount - ExpectedAmount).</summary>
    public decimal? Discrepancy { get; set; }

    public int? ClosedByUserId { get; set; }

    public string? CloseComment { get; set; }

    public CashSessionStatus Status { get; set; } = CashSessionStatus.Open;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
