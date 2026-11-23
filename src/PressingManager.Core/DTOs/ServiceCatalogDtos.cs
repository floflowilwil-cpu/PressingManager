using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

public sealed record ServiceDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<ServicePriceDto> Prices);

public sealed record CreateServiceDto(
    string Name,
    string? Description);

public sealed record UpdateServiceDto(
    int Id,
    string Name,
    string? Description);

public sealed record ServicePriceDto(
    int Id,
    int ServiceId,
    BillingUnit Unit,
    decimal Amount,
    DateTime ValidFrom,
    DateTime? ValidTo);

/// <summary>
/// Definit un nouveau tarif. Le tarif en vigueur pour la meme unite est cloture
/// (ValidTo) au lieu d'etre modifie, afin de garder l'historique des prix.
/// </summary>
public sealed record SetServicePriceDto(
    int ServiceId,
    BillingUnit Unit,
    decimal Amount,
    int ChangedByUserId);
