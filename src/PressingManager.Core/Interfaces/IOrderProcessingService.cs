using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Interfaces;

/// <summary>Suivi du traitement des commandes et historique des statuts (documentation, section 4.4).</summary>
public interface IOrderProcessingService
{
    /// <summary>
    /// Change le statut d'une commande si la transition est permise (voir OrderStatusRules),
    /// et enregistre l'evenement dans l'historique. Permission UpdateOrderStatus ; une correction
    /// (retour en arriere) demande aussi CorrectOrderStatus. Leve BusinessRuleException si la
    /// transition est refusee, PermissionDeniedException si l'utilisateur n'a pas le droit.
    /// Les annulations et les corrections sont ecrites dans le journal d'audit.
    /// </summary>
    Task<OrderStatusHistoryDto> ChangeStatusAsync(
        ChangeOrderStatusDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Historique des changements de statut, du plus ancien au plus recent (permission ViewOrders).</summary>
    Task<IReadOnlyList<OrderStatusHistoryDto>> GetStatusHistoryAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// File de traitement (permission ViewOrders) : sans statut, les commandes encore a traiter
    /// (ni livrees ni annulees) ; avec un statut, uniquement celles de ce statut.
    /// Triees par date prevue (les plus urgentes d'abord), puis par date de creation.
    /// </summary>
    Task<IReadOnlyList<ProcessingOrderDto>> GetOrdersAsync(
        OrderStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>Commandes en retard (date prevue depassee, pas encore pretes). Permission ViewOrders.</summary>
    Task<IReadOnlyList<ProcessingOrderDto>> GetOverdueOrdersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Statuts proposables a l'utilisateur depuis un statut donne (flux normal).</summary>
    IReadOnlyCollection<OrderStatus> GetAllowedNextStatuses(OrderStatus current);
}
