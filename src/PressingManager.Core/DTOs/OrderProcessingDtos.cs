using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

/// <summary>
/// Demande de changement de statut. ChangedByUserId est fourni par l'appelant
/// (l'utilisateur connecte) ; la verification des permissions releve du module authentification.
/// </summary>
public sealed record ChangeOrderStatusDto(
    int OrderId,
    OrderStatus NewStatus,
    int ChangedByUserId,
    bool IsAuthorizedCorrection = false,
    string? Comment = null);

public sealed record OrderStatusHistoryDto(
    int Id,
    int OrderId,
    OrderStatus? FromStatus,
    OrderStatus ToStatus,
    int ChangedByUserId,
    DateTime ChangedAt,
    bool IsCorrection,
    string? Comment);
