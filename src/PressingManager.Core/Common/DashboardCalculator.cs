using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Common;

/// <summary>Calculs purs du tableau de bord (sans acces a la base de donnees).</summary>
public static class DashboardCalculator
{
    /// <summary>Statuts consideres "en cours" : enregistree mais pas encore prete ni terminee.</summary>
    private static readonly OrderStatus[] InProgressStatuses =
    {
        OrderStatus.Received,
        OrderStatus.AwaitingProcessing,
        OrderStatus.Cleaning,
        OrderStatus.Ironing
    };

    /// <summary>
    /// Debut et fin (en UTC) de la journee locale contenant <paramref name="nowUtc"/>.
    /// La base stocke l'UTC, mais "aujourd'hui" doit suivre l'heure du pressing.
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) GetLocalDayRangeUtc(DateTime nowUtc, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), timeZone);
        var startLocal = DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(startLocal.AddDays(1), timeZone));
    }

    /// <summary>Produits actifs dont le stock est inferieur ou egal au seuil, les plus bas d'abord.</summary>
    /// <remarks>Chaque produit doit etre charge avec ses mouvements de stock (Include).</remarks>
    public static IReadOnlyList<StockAlert> GetStockAlerts(IEnumerable<Product> products) =>
        products
            .Where(p => p.IsActive)
            .Select(p => new StockAlert(
                p.Id, p.Name, p.Unit, StockCalculator.GetCurrentStock(p.StockMovements), p.AlertThreshold))
            .Where(a => StockCalculator.IsBelowAlertThreshold(a.CurrentStock, a.AlertThreshold))
            .OrderBy(a => a.CurrentStock)
            .ToList();

    /// <summary>
    /// Synthese du jour. Les commandes annulees sont ignorees.
    /// <paramref name="collectedToday"/> vient du module paiements (somme des paiements valides du jour).
    /// </summary>
    public static DashboardSummary Summarize(
        IEnumerable<OrderReportRow> orders,
        decimal collectedToday,
        IEnumerable<Product> products,
        DateTime dayStartUtc,
        DateTime dayEndUtc)
    {
        var active = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        var unpaid = active.Where(o => o.Balance > 0).ToList();

        return new DashboardSummary(
            OrdersToday: active.Count(o => o.CreatedAt >= dayStartUtc && o.CreatedAt < dayEndUtc),
            OrdersInProgress: active.Count(o => InProgressStatuses.Contains(o.Status)),
            OrdersReady: active.Count(o => o.Status == OrderStatus.ReadyForPickup),
            RevenueToday: collectedToday,
            UnpaidOrderCount: unpaid.Count,
            OutstandingBalance: unpaid.Sum(o => o.Balance),
            StockAlerts: GetStockAlerts(products));
    }
}
