using PressingManager.Core.Enums;

namespace PressingManager.Core.Entities;

/// <summary>Entree, sortie ou ajustement de stock pour un produit.</summary>
public class StockMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public StockMovementType Type { get; set; }

    /// <summary>Toujours strictement positive ; le sens vient de <see cref="Type"/>.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Date du mouvement, en UTC.</summary>
    public DateTime MovementDate { get; set; }

    /// <summary>Cle etrangere vers l'utilisateur (module authentification).</summary>
    public int UserId { get; set; }

    public string? Reason { get; set; }
}
