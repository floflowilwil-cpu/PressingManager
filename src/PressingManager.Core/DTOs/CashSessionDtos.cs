using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

/// <summary>Demande d'ouverture d'une session de caisse.</summary>
public sealed record OpenCashSessionDto(
    decimal OpeningAmount,
    int OpenedByUserId);

/// <summary>Demande de cloture de la session de caisse avec montant compte en especes.</summary>
public sealed record CloseCashSessionDto(
    int CashSessionId,
    decimal CountedAmount,
    int ClosedByUserId,
    string? Comment = null);

public sealed record CashSessionDto(
    int Id,
    DateTime OpenedAt,
    decimal OpeningAmount,
    int OpenedByUserId,
    DateTime? ClosedAt,
    decimal? CountedAmount,
    decimal? ExpectedAmount,
    decimal? Discrepancy,
    int? ClosedByUserId,
    string? CloseComment,
    CashSessionStatus Status);
