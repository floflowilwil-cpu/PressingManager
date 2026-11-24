using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Suivi du traitement des commandes (documentation, section 4.4).
/// Le statut de reference est Order.Status (module commandes) : chaque changement valide met a jour
/// la commande ET ajoute un evenement dans OrderStatusHistory, dans une seule sauvegarde (atomique).
/// L'historique commence au premier changement : il n'est pas reconstitue pour les commandes anterieures.
/// </summary>
public sealed class OrderProcessingService : IOrderProcessingService
{
    private const int CommentMaxLength = 500;

    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly TimeProvider _time;

    public OrderProcessingService(IDbContextFactory<PressingDbContext> dbFactory, TimeProvider time)
    {
        _dbFactory = dbFactory;
        _time = time;
    }

    public async Task<OrderStatusHistoryDto> ChangeStatusAsync(
        ChangeOrderStatusDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.NewStatus))
        {
            throw new BusinessRuleException("Le statut demandé est inconnu.");
        }

        var comment = InputValidation.OptionalText(request.Comment, "Le commentaire", CommentMaxLength);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new BusinessRuleException("Commande introuvable.");

        var userIsValid = await db.Users
            .AnyAsync(u => u.Id == request.ChangedByUserId && u.IsActive, cancellationToken);
        if (!userIsValid)
        {
            throw new BusinessRuleException("Utilisateur introuvable ou désactivé.");
        }

        var current = order.Status;

        var allowedInNormalFlow = OrderStatusRules.CanTransition(current, request.NewStatus);
        var allowed = allowedInNormalFlow
                      || OrderStatusRules.CanTransition(current, request.NewStatus, request.IsAuthorizedCorrection);

        if (!allowed)
        {
            throw new BusinessRuleException(
                $"Transition refusée pour la commande {order.OrderNumber} : {current} vers {request.NewStatus}.");
        }

        var entry = new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = current,
            ToStatus = request.NewStatus,
            ChangedByUserId = request.ChangedByUserId,
            ChangedAt = _time.GetUtcNow().UtcDateTime,
            // Une correction = une transition qui n'etait possible que grace a l'autorisation.
            IsCorrection = !allowedInNormalFlow,
            Comment = comment
        };

        order.Status = request.NewStatus;
        db.OrderStatusHistory.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(entry);
    }

    public async Task<IReadOnlyList<OrderStatusHistoryDto>> GetStatusHistoryAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var events = await db.OrderStatusHistory
            .AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .ToListAsync(cancellationToken);

        return events.Select(h => ToDto(h)).ToList();
    }

    public IReadOnlyCollection<OrderStatus> GetAllowedNextStatuses(OrderStatus current) =>
        OrderStatusRules.GetNextStatuses(current);

    private static OrderStatusHistoryDto ToDto(OrderStatusHistory h) =>
        new(h.Id, h.OrderId, h.FromStatus, h.ToStatus, h.ChangedByUserId, h.ChangedAt, h.IsCorrection, h.Comment);
}
