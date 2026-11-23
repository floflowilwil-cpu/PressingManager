using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Interfaces;

/// <summary>Catalogue des prestations et de leurs tarifs (documentation, section 4.5).</summary>
public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceDto>> GetServicesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ServiceDto?> GetByIdAsync(
        int serviceId,
        CancellationToken cancellationToken = default);

    Task<ServiceDto> CreateAsync(
        CreateServiceDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceDto> UpdateAsync(
        UpdateServiceDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Active ou desactive un service (pas de suppression : les anciennes commandes le referencent).</summary>
    Task SetActiveAsync(
        int serviceId,
        bool isActive,
        CancellationToken cancellationToken = default);

    /// <summary>Definit un nouveau tarif et cloture le precedent pour la meme unite.</summary>
    Task<ServicePriceDto> SetPriceAsync(
        SetServicePriceDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Tarif en vigueur a la date donnee (maintenant par defaut) ; null s'il n'y en a pas.</summary>
    Task<ServicePriceDto?> GetCurrentPriceAsync(
        int serviceId,
        BillingUnit unit,
        DateTime? at = null,
        CancellationToken cancellationToken = default);
}
