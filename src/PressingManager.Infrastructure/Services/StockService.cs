using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

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

    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly ICurrentUserContext _session;
    private readonly TimeProvider _time;

    public StockService(
        IDbContextFactory<PressingDbContext> dbFactory,
        ICurrentUserContext session,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _session = session;
        _time = time;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewStock);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Product> query = db.Products.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        var products = await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        var stocks = await GetStocksAsync(db, products.Select(p => p.Id).ToList(), cancellationToken);

        return products
            .Select(p => ToDto(p, stocks.TryGetValue(p.Id, out var stock) ? stock : 0m))
            .ToList();
    }

    public async Task<ProductDto?> GetProductByIdAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewStock);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var stock = await GetCurrentStockAsync(db, product.Id, cancellationToken);
        return ToDto(product, stock);
    }

    public async Task<ProductDto> CreateProductAsync(
        CreateProductDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session.Require(Permission.ManageStock);

        var name = InputValidation.RequireText(request.Name, "Le nom du produit", NameMaxLength);
        var unit = InputValidation.RequireText(request.Unit, "L'unité", UnitMaxLength);
        EnsureThresholdIsValid(request.AlertThreshold);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureNameIsAvailableAsync(db, name, ignoredProductId: null, cancellationToken);

        var product = new Product
        {
            Name = name,
            Unit = unit,
            AlertThreshold = request.AlertThreshold,
            IsActive = true
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(product, 0m);
    }

    public async Task<ProductDto> UpdateProductAsync(
        UpdateProductDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session.Require(Permission.ManageStock);

        var name = InputValidation.RequireText(request.Name, "Le nom du produit", NameMaxLength);
        var unit = InputValidation.RequireText(request.Unit, "L'unité", UnitMaxLength);
        EnsureThresholdIsValid(request.AlertThreshold);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new BusinessRuleException("Produit introuvable.");

        await EnsureNameIsAvailableAsync(db, name, product.Id, cancellationToken);

        product.Name = name;
        product.Unit = unit;
        product.AlertThreshold = request.AlertThreshold;
        await db.SaveChangesAsync(cancellationToken);

        var stock = await GetCurrentStockAsync(db, product.Id, cancellationToken);
        return ToDto(product, stock);
    }

    public async Task<StockMovementDto> RecordMovementAsync(
        RecordStockMovementDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session.Require(Permission.ManageStock);

        if (!Enum.IsDefined(request.Type))
        {
            throw new BusinessRuleException("Le type de mouvement est inconnu.");
        }

        if (request.Quantity <= 0)
        {
            throw new BusinessRuleException("La quantité doit être strictement positive.");
        }

        var reason = InputValidation.OptionalText(request.Reason, "Le motif", ReasonMaxLength);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new BusinessRuleException("Produit introuvable.");

        if (!product.IsActive)
        {
            throw new BusinessRuleException($"Le produit « {product.Name} » est désactivé.");
        }

        var userIsValid = await db.Users
            .AnyAsync(u => u.Id == request.UserId && u.IsActive, cancellationToken);
        if (!userIsValid)
        {
            throw new BusinessRuleException("Utilisateur introuvable ou désactivé.");
        }

        var removesStock = request.Type is StockMovementType.Exit or StockMovementType.AdjustmentDecrease;
        if (removesStock)
        {
            var currentStock = await GetCurrentStockAsync(db, product.Id, cancellationToken);
            if (!StockCalculator.CanRemove(currentStock, request.Quantity))
            {
                throw new BusinessRuleException(
                    $"Stock insuffisant pour « {product.Name} » : {currentStock} {product.Unit} disponible(s), {request.Quantity} demandé(s).");
            }
        }

        var movement = new StockMovement
        {
            ProductId = product.Id,
            Type = request.Type,
            Quantity = request.Quantity,
            MovementDate = _time.GetUtcNow().UtcDateTime,
            UserId = request.UserId,
            Reason = reason
        };

        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(movement);
    }

    /// <summary>Mouvements d'un produit, du plus recent au plus ancien.</summary>
    public async Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewStock);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var movements = await db.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);

        return movements.Select(m => ToDto(m)).ToList();
    }

    public async Task<IReadOnlyList<ProductDto>> GetLowStockProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await GetProductsAsync(includeInactive: false, cancellationToken);
        return products.Where(p => p.IsBelowAlertThreshold).ToList();
    }

    private static async Task<decimal> GetCurrentStockAsync(
        PressingDbContext db,
        int productId,
        CancellationToken cancellationToken)
    {
        var movements = await db.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .ToListAsync(cancellationToken);

        return StockCalculator.GetCurrentStock(movements);
    }

    private static async Task<Dictionary<int, decimal>> GetStocksAsync(
        PressingDbContext db,
        List<int> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var movements = await db.StockMovements
            .AsNoTracking()
            .Where(m => productIds.Contains(m.ProductId))
            .ToListAsync(cancellationToken);

        return movements
            .GroupBy(m => m.ProductId)
            .ToDictionary(g => g.Key, g => StockCalculator.GetCurrentStock(g));
    }

    private static async Task EnsureNameIsAvailableAsync(
        PressingDbContext db,
        string name,
        int? ignoredProductId,
        CancellationToken cancellationToken)
    {
        var existing = await db.Products
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);

        var duplicate = existing.Any(p =>
            p.Id != ignoredProductId
            && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            throw new BusinessRuleException($"Un produit nommé « {name} » existe déjà.");
        }
    }

    private static void EnsureThresholdIsValid(decimal threshold)
    {
        if (threshold < 0)
        {
            throw new BusinessRuleException("Le seuil d'alerte ne peut pas être négatif.");
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
