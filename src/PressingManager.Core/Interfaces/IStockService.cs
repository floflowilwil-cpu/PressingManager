using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>Produits, mouvements et alertes de stock (documentation, section 4.8).</summary>
public interface IStockService
{
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ProductDto?> GetProductByIdAsync(
        int productId,
        CancellationToken cancellationToken = default);

    Task<ProductDto> CreateProductAsync(
        CreateProductDto request,
        CancellationToken cancellationToken = default);

    Task<ProductDto> UpdateProductAsync(
        UpdateProductDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre une entree, une sortie ou un ajustement. Leve InvalidOperationException
    /// si la quantite n'est pas positive ou si une sortie rendrait le stock negatif.
    /// </summary>
    Task<StockMovementDto> RecordMovementAsync(
        RecordStockMovementDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(
        int productId,
        CancellationToken cancellationToken = default);

    /// <summary>Produits actifs dont le stock est inferieur ou egal au seuil d'alerte.</summary>
    Task<IReadOnlyList<ProductDto>> GetLowStockProductsAsync(
        CancellationToken cancellationToken = default);
}
