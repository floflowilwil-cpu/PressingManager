using System.Globalization;
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
    private readonly ICurrentUserContext _session;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public OrderProcessingService(
        IDbContextFactory<PressingDbContext> dbFactory,
        ICurrentUserContext session,
        IAuditService audit,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _session = session;
        _audit = audit;
        _time = time;
    }

    public async Task<OrderStatusHistoryDto> ChangeStatusAsync(
        ChangeOrderStatusDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session.Require(Permission.UpdateOrderStatus);

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

        // Retour en arriere : action sensible, reservee a qui a le droit de corriger.
        if (!allowedInNormalFlow)
        {
            _session.Require(Permission.CorrectOrderStatus);
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

        await AuditSensitiveChangeAsync(order, entry, cancellationToken);

        return ToDto(entry);
    }

    public async Task<IReadOnlyList<OrderStatusHistoryDto>> GetStatusHistoryAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewOrders);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var events = await db.OrderStatusHistory
            .AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .ToListAsync(cancellationToken);

        return events.Select(h => ToDto(h)).ToList();
    }

    public async Task<IReadOnlyList<ProcessingOrderDto>> GetOrdersAsync(
        OrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewOrders);

        if (status is { } requested && !Enum.IsDefined(requested))
        {
            throw new BusinessRuleException("Le statut demandé est inconnu.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Order> query = db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items);

        query = status is { } wanted
            ? query.Where(o => o.Status == wanted)
            : query.Where(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled);

        var orders = await query.ToListAsync(cancellationToken);
        var now = _time.GetUtcNow().UtcDateTime;

        // Tri en memoire : SQLite ne sait pas ordonner proprement les dates nullables.
        return orders
            .OrderBy(o => o.ExpectedDate ?? DateTime.MaxValue)
            .ThenBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Select(o => ToDto(o, now))
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessingOrderDto>> GetOverdueOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        var open = await GetOrdersAsync(status: null, cancellationToken);
        return open.Where(o => o.IsOverdue).ToList();
    }

    public IReadOnlyCollection<OrderStatus> GetAllowedNextStatuses(OrderStatus current) =>
        OrderStatusRules.GetNextStatuses(current);

    /// <summary>Annulations et corrections : operations sensibles tracees dans le journal d'audit (section 11).</summary>
    private async Task AuditSensitiveChangeAsync(
        Order order,
        OrderStatusHistory entry,
        CancellationToken cancellationToken)
    {
        string? action = null;

        if (entry.ToStatus == OrderStatus.Cancelled)
        {
            action = AuditActions.OrderCancelled;
        }
        else if (entry.IsCorrection)
        {
            action = AuditActions.OrderStatusCorrected;
        }

        if (action is null)
        {
            return;
        }

        var details = $"{order.OrderNumber} : {entry.FromStatus} -> {entry.ToStatus}";
        if (!string.IsNullOrEmpty(entry.Comment))
        {
            details += $" ({entry.Comment})";
        }

        await _audit.LogAsync(
            action,
            nameof(Order),
            order.Id.ToString(CultureInfo.InvariantCulture),
            details,
            entry.ChangedByUserId,
            cancellationToken);
    }

    private static ProcessingOrderDto ToDto(Order order, DateTime nowUtc) =>
        new(
            order.Id,
            order.OrderNumber,
            order.Customer?.FullName ?? string.Empty,
            order.Status,
            order.CreatedAt,
            order.ExpectedDate,
            order.Items.Sum(i => i.Quantity),
            OrderStatusRules.IsOverdue(order.Status, order.ExpectedDate, nowUtc));

    private static OrderStatusHistoryDto ToDto(OrderStatusHistory h) =>
        new(h.Id, h.OrderId, h.FromStatus, h.ToStatus, h.ChangedByUserId, h.ChangedAt, h.IsCorrection, h.Comment);
}
