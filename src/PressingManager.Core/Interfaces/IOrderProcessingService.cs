using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Interfaces;

/// <summary>Suivi du traitement des commandes et historique des statuts (documentation, section 4.4).</summary>
public interface IOrderProcessingService
{
    /// <summary>
    /// Change le statut d'une commande si la transition est permise (voir OrderStatusRules),
    /// et enregistre l'evenement dans l'historique. Leve InvalidOperationException si la
    /// transition est refusee.
    /// </summary>
    Task<OrderStatusHistoryDto> ChangeStatusAsync(
        ChangeOrderStatusDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Historique des changements de statut, du plus ancien au plus recent.</summary>
    Task<IReadOnlyList<OrderStatusHistoryDto>> GetStatusHistoryAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>Statuts proposables a l'utilisateur depuis un statut donne (flux normal).</summary>
    IReadOnlyCollection<OrderStatus> GetAllowedNextStatuses(OrderStatus current);
}
