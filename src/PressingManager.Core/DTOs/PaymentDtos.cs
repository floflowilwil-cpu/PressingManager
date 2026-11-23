using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

/// <summary>
/// Demande d'enregistrement d'un paiement client.
/// ReceivedByUserId est fourni par l'appelant (utilisateur connecte).
/// </summary>
public sealed record RegisterPaymentDto(
    int OrderId,
    decimal Amount,
    PaymentMethod Method,
    int ReceivedByUserId,
    string? Reference = null);

/// <summary>
/// Demande d'annulation ou de remboursement d'un paiement (motif obligatoire).
/// La verification du role (Admin ou Gerant) est exigee cote service/authentification.
/// </summary>
public sealed record CancelPaymentDto(
    int PaymentId,
    string Reason,
    int CancelledByUserId);

public sealed record PaymentDto(
    int Id,
    int OrderId,
    decimal Amount,
    DateTime PaidAt,
    PaymentMethod Method,
    string? Reference,
    PaymentStatus Status,
    int ReceivedByUserId,
    int? CashSessionId,
    string? CancelReason,
    int? CancelledByUserId,
    DateTime? CancelledAt);
