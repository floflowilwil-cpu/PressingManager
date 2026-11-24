using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Suivi du traitement des commandes (documentation, section 4.4).
/// Le statut courant d'une commande est celui du dernier evenement de OrderStatusHistory ;
/// sans historique, la commande est consideree comme Received (statut initial).
/// TODO (integration avec le module commandes) : verifier que la commande existe et
/// recopier le nouveau statut sur Order dans le meme SaveChanges.
/// </summary>
public sealed class OrderProcessingService : IOrderProcessingService
{
    private const int CommentMaxLength = 500;

    private readonly DbContext _db;
    private readonly TimeProvider _clock;

    public OrderProcessingService(DbContext db, TimeProvider? clock = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<OrderStatusHistoryDto> ChangeStatusAsync(
        ChangeOrderStatusDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.NewStatus))
        {
            throw new ArgumentException("Le statut demandé est inconnu.", nameof(request));
        }

        var comment = InputValidation.OptionalText(request.Comment, "Le commentaire", CommentMaxLength);

        var lastEvent = await _db.Set<OrderStatusHistory>()
            .AsNoTracking()
            .Where(h => h.OrderId == request.OrderId)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var current = lastEvent?.ToStatus ?? OrderStatus.Received;

        var allowedInNormalFlow = OrderStatusRules.CanTransition(current, request.NewStatus);
        var allowed = allowedInNormalFlow
                      || OrderStatusRules.CanTransition(current, request.NewStatus, request.IsAuthorizedCorrection);

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Transition refusée pour la commande {request.OrderId} : {current} vers {request.NewStatus}.");
        }

        var entry = new OrderStatusHistory
        {
            OrderId = request.OrderId,
            FromStatus = current,
            ToStatus = request.NewStatus,
            ChangedByUserId = request.ChangedByUserId,
            ChangedAt = _clock.GetUtcNow().UtcDateTime,
            // Une correction = une transition qui n'etait possible que grace a l'autorisation.
            IsCorrection = !allowedInNormalFlow,
            Comment = comment
        };

        _db.Set<OrderStatusHistory>().Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(entry);
    }

    public async Task<IReadOnlyList<OrderStatusHistoryDto>> GetStatusHistoryAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var events = await _db.Set<OrderStatusHistory>()
            .AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .ToListAsync(cancellationToken);

        return events.Select(x => ToDto(x)).ToList();
    }

    public IReadOnlyCollection<OrderStatus> GetAllowedNextStatuses(OrderStatus current) =>
        OrderStatusRules.GetNextStatuses(current);

    private static OrderStatusHistoryDto ToDto(OrderStatusHistory h) =>
        new(h.Id, h.OrderId, h.FromStatus, h.ToStatus, h.ChangedByUserId, h.ChangedAt, h.IsCorrection, h.Comment);
}
