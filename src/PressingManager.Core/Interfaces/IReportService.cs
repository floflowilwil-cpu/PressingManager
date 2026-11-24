using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Interfaces;

/// <summary>
/// Contrat des rapports. L'implementation EF Core sera ajoutee quand les entites
/// Order / Payment / Expense (Personnes 2 et 4) seront fusionnees dans develop ;
/// les calculs, eux, sont deja dans <see cref="Common.ReportCalculator"/>.
/// </summary>
public interface IReportService
{
    Task<IReadOnlyList<OrderReportRow>> GetOrdersAsync(
        DateTime fromUtc, DateTime toUtc, OrderStatus? status = null, CancellationToken cancellationToken = default);

    Task<FinancialSummary> GetFinancialSummaryAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Commandes avec un solde restant du (hors annulees).</summary>
    Task<IReadOnlyList<OrderReportRow>> GetOutstandingBalancesAsync(CancellationToken cancellationToken = default);

    /// <summary>Commandes pretes mais pas encore recuperees.</summary>
    Task<IReadOnlyList<OrderReportRow>> GetAwaitingPickupAsync(CancellationToken cancellationToken = default);
}
