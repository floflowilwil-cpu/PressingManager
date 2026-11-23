namespace PressingManager.Core.Enums;

/// <summary>
/// Type de mouvement de stock. La quantite d'un mouvement est toujours positive ;
/// c'est le type qui determine si elle s'ajoute ou se retire du stock.
/// </summary>
public enum StockMovementType
{
    /// <summary>Entree de stock (achat, reapprovisionnement).</summary>
    Entry = 0,

    /// <summary>Sortie de stock (consommation).</summary>
    Exit = 1,

    /// <summary>Ajustement a la hausse (inventaire).</summary>
    AdjustmentIncrease = 2,

    /// <summary>Ajustement a la baisse (inventaire, perte, casse).</summary>
    AdjustmentDecrease = 3
}
