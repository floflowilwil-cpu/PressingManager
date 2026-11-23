using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

/// <summary>Produit avec son stock disponible calcule a partir des mouvements.</summary>
public sealed record ProductDto(
    int Id,
    string Name,
    string Unit,
    decimal AlertThreshold,
    bool IsActive,
    decimal CurrentStock,
    bool IsBelowAlertThreshold);

public sealed record CreateProductDto(
    string Name,
    string Unit,
    decimal AlertThreshold);

public sealed record UpdateProductDto(
    int Id,
    string Name,
    string Unit,
    decimal AlertThreshold);

/// <summary>La quantite doit etre strictement positive ; le sens vient du type de mouvement.</summary>
public sealed record RecordStockMovementDto(
    int ProductId,
    StockMovementType Type,
    decimal Quantity,
    int UserId,
    string? Reason = null);

public sealed record StockMovementDto(
    int Id,
    int ProductId,
    StockMovementType Type,
    decimal Quantity,
    DateTime MovementDate,
    int UserId,
    string? Reason);
