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
}
