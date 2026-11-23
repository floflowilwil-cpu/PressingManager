using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

/// <summary>Demande d'enregistrement d'une depense du pressing.</summary>
public sealed record CreateExpenseDto(
    DateTime Date,
    string Category,
    decimal Amount,
    PaymentMethod Method,
    int CreatedByUserId,
    string? Comment = null);

public sealed record ExpenseDto(
    int Id,
    DateTime Date,
    string Category,
    decimal Amount,
    PaymentMethod Method,
    string? Comment,
    int CreatedByUserId,
    int? CashSessionId,
    bool IsCancelled,
    string? CancelReason);
