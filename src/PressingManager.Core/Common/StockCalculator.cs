using PressingManager.Core.Entities;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Common;

/// <summary>Calculs de stock purs (sans acces a la base de donnees).</summary>
public static class StockCalculator
{
    /// <summary>Quantite signee d'un mouvement : positive pour une entree, negative pour une sortie.</summary>
    public static decimal GetSignedQuantity(StockMovement movement) => movement.Type switch
    {
        StockMovementType.Entry or StockMovementType.AdjustmentIncrease => movement.Quantity,
        StockMovementType.Exit or StockMovementType.AdjustmentDecrease => -movement.Quantity,
        _ => throw new ArgumentOutOfRangeException(nameof(movement), movement.Type, "Type de mouvement inconnu.")
    };

    /// <summary>Stock disponible = somme des mouvements signes.</summary>
    public static decimal GetCurrentStock(IEnumerable<StockMovement> movements) =>
        movements.Sum(GetSignedQuantity);

    /// <summary>Alerte de stock : stock inferieur ou egal au seuil.</summary>
    public static bool IsBelowAlertThreshold(decimal currentStock, decimal alertThreshold) =>
        currentStock <= alertThreshold;

    /// <summary>Une sortie est valide si elle est positive et ne rend pas le stock negatif.</summary>
    public static bool CanRemove(decimal currentStock, decimal quantity) =>
        quantity > 0 && quantity <= currentStock;
}
