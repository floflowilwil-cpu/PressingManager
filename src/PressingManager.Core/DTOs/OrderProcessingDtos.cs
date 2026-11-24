using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

/// <summary>
/// Demande de changement de statut. ChangedByUserId est fourni par l'appelant (l'utilisateur connecte).
/// Les permissions sont controlees par le service : UpdateOrderStatus, et en plus CorrectOrderStatus
/// lorsque la transition n'est possible que comme correction autorisee.
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

/// <summary>
/// Ligne de la file de traitement (liste des commandes par etat).
/// IsOverdue : la date prevue est depassee alors que la commande n'est pas encore prete.
/// </summary>
public sealed record ProcessingOrderDto(
    int OrderId,
    string OrderNumber,
    string CustomerName,
    OrderStatus Status,
    DateTime CreatedAt,
    DateTime? ExpectedDate,
    int ItemCount,
    bool IsOverdue)
{
    public bool IsReadyForPickup => Status == OrderStatus.ReadyForPickup;
}
