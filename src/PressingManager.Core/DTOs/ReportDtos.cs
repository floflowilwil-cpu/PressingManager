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
