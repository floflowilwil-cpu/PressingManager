using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Produits, mouvements et alertes de stock (documentation, section 4.8).
/// Le stock n'est jamais stocke : il est recalcule a partir des mouvements avec StockCalculator.
/// Le calcul se fait en memoire car le fournisseur SQLite ne sait pas faire de SUM sur des decimal.
/// </summary>
public sealed class StockService : IStockService
{
    private const int NameMaxLength = 100;
    private const int UnitMaxLength = 20;
    private const int ReasonMaxLength = 250;

    private readonly DbContext _db;
    private readonly TimeProvider _clock;

    public StockService(DbContext db, TimeProvider? clock = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Product> query = _db.Set<Product>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        var products = await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        var stocks = await GetStocksAsync(products.Select(p => p.Id).ToList(), cancellationToken);

        return products
            .Select(p => ToDto(p, stocks.TryGetValue(p.Id, out var stock) ? stock : 0m))
            .ToList();
    }

    public async Task<ProductDto?> GetProductByIdAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var product = await _db.Set<Product>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var stock = await GetCurrentStockAsync(product.Id, cancellationToken);
        return ToDto(product, stock);
    }

    public async Task<ProductDto> CreateProductAsync(
        CreateProductDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = InputValidation.RequireText(request.Name, "Le nom du produit", NameMaxLength);
        var unit = InputValidation.RequireText(request.Unit, "L'unité", UnitMaxLength);
        EnsureThresholdIsValid(request.AlertThreshold);

        await EnsureNameIsAvailableAsync(name, ignoredProductId: null, cancellationToken);

        var product = new Product
        {
            Name = name,
            Unit = unit,
            AlertThreshold = request.AlertThreshold,
            IsActive = true
        };

        _db.Set<Product>().Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(product, 0m);
    }

    public async Task<ProductDto> UpdateProductAsync(
        UpdateProductDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = InputValidation.RequireText(request.Name, "Le nom du produit", NameMaxLength);
        var unit = InputValidation.RequireText(request.Unit, "L'unité", UnitMaxLength);
        EnsureThresholdIsValid(request.AlertThreshold);

        var product = await _db.Set<Product>()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Le produit {request.Id} est introuvable.");

        await EnsureNameIsAvailableAsync(name, product.Id, cancellationToken);

        product.Name = name;
        product.Unit = unit;
        product.AlertThreshold = request.AlertThreshold;
        await _db.SaveChangesAsync(cancellationToken);

        var stock = await GetCurrentStockAsync(product.Id, cancellationToken);
        return ToDto(product, stock);
    }

    public async Task<StockMovementDto> RecordMovementAsync(
        RecordStockMovementDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.Type))
        {
            throw new ArgumentException("Le type de mouvement est inconnu.", nameof(request));
        }

        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("La quantité doit être strictement positive.");
        }

        var reason = InputValidation.OptionalText(request.Reason, "Le motif", ReasonMaxLength);

        var product = await _db.Set<Product>()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Le produit {request.ProductId} est introuvable.");

        if (!product.IsActive)
        {
            throw new InvalidOperationException($"Le produit « {product.Name} » est désactivé.");
        }

        var removesStock = request.Type is StockMovementType.Exit or StockMovementType.AdjustmentDecrease;
        if (removesStock)
        {
            var currentStock = await GetCurrentStockAsync(product.Id, cancellationToken);
            if (!StockCalculator.CanRemove(currentStock, request.Quantity))
            {
                throw new InvalidOperationException(
                    $"Stock insuffisant pour « {product.Name} » : {currentStock} {product.Unit} disponible(s), {request.Quantity} demandé(s).");
            }
        }

        var movement = new StockMovement
        {
            ProductId = product.Id,
            Type = request.Type,
            Quantity = request.Quantity,
            MovementDate = _clock.GetUtcNow().UtcDateTime,
            UserId = request.UserId,
            Reason = reason
        };

        _db.Set<StockMovement>().Add(movement);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(movement);
    }

    /// <summary>Mouvements d'un produit, du plus recent au plus ancien.</summary>
    public async Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var movements = await _db.Set<StockMovement>()
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);

        return movements.Select(x => ToDto(x)).ToList();
    }

    public async Task<IReadOnlyList<ProductDto>> GetLowStockProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await GetProductsAsync(includeInactive: false, cancellationToken);
        return products.Where(p => p.IsBelowAlertThreshold).ToList();
    }

    private async Task<decimal> GetCurrentStockAsync(int productId, CancellationToken cancellationToken)
    {
        var movements = await _db.Set<StockMovement>()
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .ToListAsync(cancellationToken);

        return StockCalculator.GetCurrentStock(movements);
    }

    private async Task<Dictionary<int, decimal>> GetStocksAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var ids = productIds.ToList();
        var movements = await _db.Set<StockMovement>()
            .AsNoTracking()
            .Where(m => ids.Contains(m.ProductId))
            .ToListAsync(cancellationToken);

        return movements
            .GroupBy(m => m.ProductId)
            .ToDictionary(g => g.Key, g => StockCalculator.GetCurrentStock(g));
    }

    private async Task EnsureNameIsAvailableAsync(string name, int? ignoredProductId, CancellationToken cancellationToken)
    {
        var existing = await _db.Set<Product>()
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        var duplicate = existing.Any(p =>
            p.Id != ignoredProductId
            && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            throw new InvalidOperationException($"Un produit nommé « {name} » existe déjà.");
        }
    }

    private static void EnsureThresholdIsValid(decimal threshold)
    {
        if (threshold < 0)
        {
            throw new ArgumentException("Le seuil d'alerte ne peut pas être négatif.", nameof(threshold));
        }
    }

    private static ProductDto ToDto(Product product, decimal currentStock) =>
        new(
            product.Id,
            product.Name,
            product.Unit,
            product.AlertThreshold,
            product.IsActive,
            currentStock,
            StockCalculator.IsBelowAlertThreshold(currentStock, product.AlertThreshold));

    private static StockMovementDto ToDto(StockMovement movement) =>
        new(
            movement.Id,
            movement.ProductId,
            movement.Type,
            movement.Quantity,
            movement.MovementDate,
            movement.UserId,
            movement.Reason);
}
