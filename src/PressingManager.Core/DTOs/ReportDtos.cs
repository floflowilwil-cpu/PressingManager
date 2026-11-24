using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

/// <summary>Une commande vue par les rapports (donnees deja agregees : total et montant paye).</summary>
public sealed record OrderReportRow(
    string OrderNumber,
    DateTime CreatedAt,
    string CustomerName,
    OrderStatus Status,
    decimal Total,
    decimal AmountPaid)
{
    public decimal Balance => Total - AmountPaid;
}

/// <summary>Synthese financiere d'une periode.</summary>
public sealed record FinancialSummary(
    int OrderCount,
    decimal TotalBilled,
    decimal TotalCollected,
    decimal TotalExpenses,
    decimal OutstandingBalance)
{
    public decimal NetResult => TotalCollected - TotalExpenses;
}


/// <summary>
/// Un point du graphique d'evolution : encaissements et depenses d'un jour ou d'un mois.
/// <paramref name="Period"/> est le jour (ou le 1er du mois si <paramref name="IsMonthly"/>) en heure locale ;
/// l'ecran en fait le libelle dans la langue courante.
/// </summary>
public sealed record TrendPoint(DateTime Period, bool IsMonthly, decimal Collected, decimal Expenses);

/// <summary>Part d'un service (nettoyage, repassage...) dans le chiffre d'affaires facture de la periode.</summary>
public sealed record ServiceShare(string ServiceName, decimal Amount);
