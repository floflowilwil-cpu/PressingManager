using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Common;

/// <summary>Calculs de rapports purs (sans acces a la base de donnees).</summary>
public static class ReportCalculator
{
    /// <summary>Nombre de commandes par statut ; tous les statuts sont presents (0 si aucun).</summary>
    public static IReadOnlyDictionary<OrderStatus, int> CountByStatus(IEnumerable<OrderReportRow> rows)
    {
        var counts = Enum.GetValues<OrderStatus>().ToDictionary(status => status, _ => 0);

        foreach (var row in rows)
        {
            counts[row.Status]++;
        }

        return counts;
    }

    /// <summary>Commandes non annulees avec un solde positif, plus gros solde d'abord.</summary>
    public static IReadOnlyList<OrderReportRow> GetOutstanding(IEnumerable<OrderReportRow> rows) =>
        rows.Where(r => r.Status != OrderStatus.Cancelled && r.Balance > 0)
            .OrderByDescending(r => r.Balance)
            .ToList();

    /// <summary>Commandes pretes a etre recuperees, la plus ancienne d'abord.</summary>
    public static IReadOnlyList<OrderReportRow> GetAwaitingPickup(IEnumerable<OrderReportRow> rows) =>
        rows.Where(r => r.Status == OrderStatus.ReadyForPickup)
            .OrderBy(r => r.CreatedAt)
            .ToList();

    /// <summary>
    /// Synthese d'une periode. Les commandes annulees sont exclues du facture et des soldes.
    /// <paramref name="totalCollected"/> et <paramref name="totalExpenses"/> viennent des modules paiements et depenses.
    /// </summary>
    public static FinancialSummary Summarize(
        IEnumerable<OrderReportRow> orders, decimal totalCollected, decimal totalExpenses)
    {
        var active = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        return new FinancialSummary(
            OrderCount: active.Count,
            TotalBilled: active.Sum(o => o.Total),
            TotalCollected: totalCollected,
            TotalExpenses: totalExpenses,
            OutstandingBalance: active.Where(o => o.Balance > 0).Sum(o => o.Balance));
    }

    /// <summary>
    /// Regroupe encaissements et depenses (dates UTC) par jour local si la periode fait 31 jours au plus,
    /// sinon par mois local. [fromUtc, toUtc[ est semi-ouvert ; chaque jour / mois est present, meme a zero.
    /// </summary>
    public static IReadOnlyList<TrendPoint> BuildTrend(
        IEnumerable<(DateTime Date, decimal Amount)> payments,
        IEnumerable<(DateTime Date, decimal Amount)> expenses,
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo timeZone)
    {
        var first = ToLocal(fromUtc, timeZone).Date;
        var last = ToLocal(toUtc.AddTicks(-1), timeZone).Date;
        if (last < first)
        {
            last = first;
        }

        var daily = (last - first).TotalDays < 31;

        DateTime KeyOf(DateTime local) => daily ? local.Date : new DateTime(local.Year, local.Month, 1);

        var collected = payments
            .GroupBy(p => KeyOf(ToLocal(p.Date, timeZone)))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var spent = expenses
            .GroupBy(e => KeyOf(ToLocal(e.Date, timeZone)))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var points = new List<TrendPoint>();
        for (var key = KeyOf(first); key <= last; key = daily ? key.AddDays(1) : key.AddMonths(1))
        {
            points.Add(new TrendPoint(key, !daily, collected.GetValueOrDefault(key), spent.GetValueOrDefault(key)));
        }

        return points;
    }

    private static DateTime ToLocal(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
}
