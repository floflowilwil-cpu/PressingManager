using PressingManager.Core.Enums;

namespace PressingManager.Core.Entities;

/// <summary>
/// Paiement enregistre pour une commande.
/// OrderId et ReceivedByUserId / CancelledByUserId sont de simples cles etrangeres :
/// les entites Order et User sont fournies par d'autres modules.
/// Tous les montants monetaires utilisent decimal (jamais float/double).
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    /// <summary>Montant encaisse en decimal (strictement positif).</summary>
    public decimal Amount { get; set; }

    /// <summary>Date et heure du paiement, en UTC.</summary>
    public DateTime PaidAt { get; set; }

    public PaymentMethod Method { get; set; }

    /// <summary>Numero de transaction (obligatoire et unique pour Mobile Money).</summary>
    public string? Reference { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Valid;

    public int ReceivedByUserId { get; set; }

    /// <summary>Session de caisse associee lorsque Method == Cash.</summary>
    public int? CashSessionId { get; set; }

    public CashSession? CashSession { get; set; }

    /// <summary>Motif obligatoire en cas d'annulation ou de remboursement.</summary>
    public string? CancelReason { get; set; }

    public int? CancelledByUserId { get; set; }

    /// <summary>Date et heure de l'annulation ou du remboursement, en UTC.</summary>
    public DateTime? CancelledAt { get; set; }
}
