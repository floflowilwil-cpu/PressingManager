namespace PressingManager.Core.DTOs;

/// <summary>
/// Une alerte de stock : stock disponible inférieur ou égal au seuil.
/// </summary>
public sealed record StockAlert(
    int ProductId,
    string ProductName,
    string Unit,
    decimal CurrentStock,
    decimal AlertThreshold);

/// <summary>
/// Chiffres affichés sur le tableau de bord.
/// </summary>
public sealed record DashboardSummary(
    int OrdersToday,
    int OrdersInProgress,
    int OrdersReady,
    decimal RevenueToday,
    int UnpaidOrderCount,
    decimal OutstandingBalance,
    IReadOnlyList<StockAlert> StockAlerts)
{
    /// <summary>Nombre total de clients actifs.</summary>
    public int ActiveCustomerCount { get; init; }

    /// <summary>Nombre total de commandes non annulées.</summary>
    public int TotalOrderCount { get; init; }
}