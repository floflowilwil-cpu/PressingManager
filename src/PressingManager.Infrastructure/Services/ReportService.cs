using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Rapports (documentation, section 4.10). Permission requise : ViewReports.
/// Les périodes sont des intervalles UTC semi-ouverts [fromUtc, toUtc[ ; l'écran les construit
/// avec <see cref="ReportPeriod.ToUtcRange"/>.
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly ICurrentUserContext _session;

    public ReportService(IDbContextFactory<PressingDbContext> dbFactory, ICurrentUserContext session)
    {
        _dbFactory = dbFactory;
        _session = session;
    }

    public async Task<IReadOnlyList<OrderReportRow>> GetOrdersAsync(
        DateTime fromUtc, DateTime toUtc, OrderStatus? status = null, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewReports);
        ValidateRange(fromUtc, toUtc);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var orders = db.Orders.Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc);
        if (status is { } wanted)
        {
            orders = orders.Where(o => o.Status == wanted);
        }

        return await OrderReportQueries.LoadRowsAsync(db, orders, cancellationToken);
    }

    public async Task<FinancialSummary> GetFinancialSummaryAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewReports);
        ValidateRange(fromUtc, toUtc);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await OrderReportQueries.LoadRowsAsync(
            db, db.Orders.Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc), cancellationToken);

        var collected = await OrderReportQueries.SumCollectedAsync(db, fromUtc, toUtc, cancellationToken);
        var expenses = await OrderReportQueries.SumExpensesAsync(db, fromUtc, toUtc, cancellationToken);

        return ReportCalculator.Summarize(rows, collected, expenses);
    }

    public async Task<IReadOnlyList<OrderReportRow>> GetOutstandingBalancesAsync(
        CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewReports);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await OrderReportQueries.LoadRowsAsync(
            db, db.Orders.Where(o => o.Status != OrderStatus.Cancelled), cancellationToken);

        return ReportCalculator.GetOutstanding(rows);
    }

    public async Task<IReadOnlyList<OrderReportRow>> GetAwaitingPickupAsync(
        CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewReports);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await OrderReportQueries.LoadRowsAsync(
            db, db.Orders.Where(o => o.Status == OrderStatus.ReadyForPickup), cancellationToken);

        return ReportCalculator.GetAwaitingPickup(rows);
    }

    public async Task<IReadOnlyList<TrendPoint>> GetTrendAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewReports);
        ValidateRange(fromUtc, toUtc);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var payments = await db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Valid && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync(cancellationToken);

        var expenses = await db.Expenses
            .AsNoTracking()
            .Where(e => !e.IsCancelled && e.Date >= fromUtc && e.Date < toUtc)
            .Select(e => new { e.Date, e.Amount })
            .ToListAsync(cancellationToken);

        return ReportCalculator.BuildTrend(
            payments.Select(p => (p.PaidAt, p.Amount)),
            expenses.Select(e => (e.Date, e.Amount)),
            fromUtc,
            toUtc,
            TimeZoneInfo.Local);
    }

    public async Task<IReadOnlyList<ServiceShare>> GetServiceBreakdownAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewReports);
        ValidateRange(fromUtc, toUtc);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc && o.Status != OrderStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var names = await db.Services
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        return orders
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ServiceId)
            .Select(g => new ServiceShare(
                names.GetValueOrDefault(g.Key) ?? "Autres",
                g.Sum(i => i.LineTotal)))
            .Where(s => s.Amount > 0)
            .OrderByDescending(s => s.Amount)
            .ToList();
    }

    private static void ValidateRange(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc < fromUtc)
        {
            throw new BusinessRuleException("La date de fin doit être postérieure ou égale à la date de début.");
        }
    }
}
