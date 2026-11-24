using Microsoft.EntityFrameworkCore;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Requêtes communes à ReportService et DashboardService (module Personne 5).
/// Les sommes d'argent sont calculées en mémoire : le fournisseur SQLite ne sait pas
/// additionner des decimal côté base.
/// </summary>
internal static class OrderReportQueries
{
    /// <summary>Transforme des commandes en lignes de rapport (total calculé + somme des paiements valides).</summary>
    public static async Task<List<OrderReportRow>> LoadRowsAsync(
        PressingDbContext db, IQueryable<Order> orders, CancellationToken cancellationToken)
    {
        var loaded = await orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        if (loaded.Count == 0)
        {
            return new List<OrderReportRow>();
        }

        var orderIds = loaded.Select(o => o.Id).ToList();

        var payments = await db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Valid && orderIds.Contains(p.OrderId))
            .Select(p => new { p.OrderId, p.Amount })
            .ToListAsync(cancellationToken);

        var paidByOrder = payments
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        return loaded
            .Select(o => new OrderReportRow(
                o.OrderNumber,
                DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc),
                o.Customer?.FullName ?? "(client inconnu)",
                o.Status,
                o.Total,
                paidByOrder.GetValueOrDefault(o.Id)))
            .ToList();
    }

    /// <summary>Somme des paiements valides encaissés dans [fromUtc, toUtc[.</summary>
    public static async Task<decimal> SumCollectedAsync(
        PressingDbContext db, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var amounts = await db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Valid && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .Select(p => p.Amount)
            .ToListAsync(cancellationToken);

        return amounts.Sum();
    }

    /// <summary>Somme des dépenses non annulées dans [fromUtc, toUtc[.</summary>
    public static async Task<decimal> SumExpensesAsync(
        PressingDbContext db, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var amounts = await db.Expenses
            .AsNoTracking()
            .Where(e => !e.IsCancelled && e.Date >= fromUtc && e.Date < toUtc)
            .Select(e => e.Amount)
            .ToListAsync(cancellationToken);

        return amounts.Sum();
    }
}
