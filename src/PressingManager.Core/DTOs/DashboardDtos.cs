namespace PressingManager.Core.DTOs;

/// <summary>Une alerte de stock : stock disponible inferieur ou egal au seuil.</summary>
public sealed record StockAlert(
    int ProductId,
    string ProductName,
    string Unit,
    decimal CurrentStock,
    decimal AlertThreshold);

/// <summary>Chiffres affiches sur le tableau de bord (documentation, section 4.1).</summary>
public sealed record DashboardSummary(
    int OrdersToday,
    int OrdersInProgress,
    int OrdersReady,
    decimal RevenueToday,
    int UnpaidOrderCount,
    decimal OutstandingBalance,
    IReadOnlyList<StockAlert> StockAlerts);
