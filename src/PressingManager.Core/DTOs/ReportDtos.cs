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


/// <summary>Un point du graphique d'evolution : encaissements et depenses d'un jour ou d'un mois.</summary>
public sealed record TrendPoint(string Label, decimal Collected, decimal Expenses);

/// <summary>Part d'un service (nettoyage, repassage...) dans le chiffre d'affaires facture de la periode.</summary>
public sealed record ServiceShare(string ServiceName, decimal Amount);
