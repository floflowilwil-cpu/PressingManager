namespace PressingManager.Core.Entities;

/// <summary>
/// Produit consommable du pressing (detergent, cintres, sachets...).
/// Le stock disponible n'est pas stocke : il se calcule a partir des mouvements.
/// </summary>
public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Unite d'affichage : "kg", "L", "unite"...</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>Une alerte est levee quand le stock est inferieur ou egal a ce seuil.</summary>
    public decimal AlertThreshold { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
