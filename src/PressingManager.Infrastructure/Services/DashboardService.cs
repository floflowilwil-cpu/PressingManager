using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Indicateurs du tableau de bord (documentation, section 4.1). Permission requise : ViewDashboard.
/// Les alertes de stock viennent du module stock (Personne 3) via <see cref="IStockService"/> :
/// tant qu'il n'est pas enregistré, ou si l'utilisateur n'a pas le droit de voir le stock, la liste est vide.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly ICurrentUserContext _session;
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _timeZone;
    private readonly IStockService? _stock;

    public DashboardService(
        IDbContextFactory<PressingDbContext> dbFactory,
        ICurrentUserContext session,
        TimeProvider time,
        IStockService? stock = null,
        TimeZoneInfo? timeZone = null)
    {
        _dbFactory = dbFactory;
        _session = session;
        _time = time;
        _stock = stock;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewDashboard);

        var (dayStartUtc, dayEndUtc) = DashboardCalculator.GetLocalDayRangeUtc(_time.GetUtcNow().UtcDateTime, _timeZone);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await OrderReportQueries.LoadRowsAsync(
            db, db.Orders.Where(o => o.Status != OrderStatus.Cancelled), cancellationToken);

        var collectedToday = await OrderReportQueries.SumCollectedAsync(db, dayStartUtc, dayEndUtc, cancellationToken);

        var summary = DashboardCalculator.Summarize(rows, collectedToday, Array.Empty<Product>(), dayStartUtc, dayEndUtc);

        return summary with { StockAlerts = await GetStockAlertsAsync(cancellationToken) };
    }

    private async Task<IReadOnlyList<StockAlert>> GetStockAlertsAsync(CancellationToken cancellationToken)
    {
        if (_stock is null)
        {
            return Array.Empty<StockAlert>();
        }

        try
        {
            var lowStock = await _stock.GetLowStockProductsAsync(cancellationToken);

            return lowStock
                .Select(p => new StockAlert(p.Id, p.Name, p.Unit, p.CurrentStock, p.AlertThreshold))
                .OrderBy(a => a.CurrentStock)
                .ToList();
        }
        catch (PermissionDeniedException)
        {
            // Ex. caissier : il voit le tableau de bord mais pas le stock.
            return Array.Empty<StockAlert>();
        }
    }
}
