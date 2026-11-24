using Microsoft.EntityFrameworkCore;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Catalogue des prestations et de leurs tarifs (documentation, section 4.5).
/// Depend d'un DbContext generique : le PressingDbContext partage doit exposer les entites
/// Service et ServicePrice (voir Data/Configurations).
/// Erreurs : ArgumentException pour une saisie invalide, InvalidOperationException pour
/// une regle metier refusee (doublon, service introuvable...).
/// </summary>
public sealed class ServiceCatalogService : IServiceCatalogService
{
    private const int NameMaxLength = 100;
    private const int DescriptionMaxLength = 500;

    private readonly DbContext _db;
    private readonly TimeProvider _clock;

    public ServiceCatalogService(DbContext db, TimeProvider? clock = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<ServiceDto>> GetServicesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Service> query = _db.Set<Service>().AsNoTracking().Include(s => s.Prices);

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var services = await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);
        return services.Select(x => ToDto(x)).ToList();
    }

    public async Task<ServiceDto?> GetByIdAsync(
        int serviceId,
        CancellationToken cancellationToken = default)
    {
        var service = await _db.Set<Service>()
            .AsNoTracking()
            .Include(s => s.Prices)
            .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken);

        return service is null ? null : ToDto(service);
    }

    public async Task<ServiceDto> CreateAsync(
        CreateServiceDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = InputValidation.RequireText(request.Name, "Le nom du service", NameMaxLength);
        var description = InputValidation.OptionalText(request.Description, "La description", DescriptionMaxLength);

        await EnsureNameIsAvailableAsync(name, ignoredServiceId: null, cancellationToken);

        var service = new Service
        {
            Name = name,
            Description = description,
            IsActive = true
        };

        _db.Set<Service>().Add(service);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(service);
    }

    public async Task<ServiceDto> UpdateAsync(
        UpdateServiceDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = InputValidation.RequireText(request.Name, "Le nom du service", NameMaxLength);
        var description = InputValidation.OptionalText(request.Description, "La description", DescriptionMaxLength);

        var service = await _db.Set<Service>()
            .Include(s => s.Prices)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Le service {request.Id} est introuvable.");

        await EnsureNameIsAvailableAsync(name, service.Id, cancellationToken);

        service.Name = name;
        service.Description = description;
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(service);
    }

    public async Task SetActiveAsync(
        int serviceId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var service = await _db.Set<Service>()
            .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken)
            ?? throw new InvalidOperationException($"Le service {serviceId} est introuvable.");

        if (service.IsActive == isActive)
        {
            return;
        }

        service.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ServicePriceDto> SetPriceAsync(
        SetServicePriceDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Le tarif doit être strictement positif.", nameof(request));
        }

        if (!Enum.IsDefined(request.Unit))
        {
            throw new ArgumentException("L'unité de facturation est inconnue.", nameof(request));
        }

        var serviceExists = await _db.Set<Service>()
            .AnyAsync(s => s.Id == request.ServiceId, cancellationToken);

        if (!serviceExists)
        {
            throw new InvalidOperationException($"Le service {request.ServiceId} est introuvable.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        // Tarifs encore ouverts (ValidTo = null) pour ce service et cette unite.
        var openPrices = await _db.Set<ServicePrice>()
            .Where(p => p.ServiceId == request.ServiceId && p.Unit == request.Unit && p.ValidTo == null)
            .ToListAsync(cancellationToken);

        // Meme montant que le tarif en vigueur : rien a changer, on garde l'historique propre.
        var unchanged = openPrices.FirstOrDefault(p => p.Amount == request.Amount && p.ValidFrom <= now);
        if (unchanged is not null)
        {
            return ToDto(unchanged);
        }

        // Cloture + creation dans le meme SaveChanges : l'operation est atomique.
        foreach (var open in openPrices)
        {
            open.ValidTo = now;
        }

        var price = new ServicePrice
        {
            ServiceId = request.ServiceId,
            Unit = request.Unit,
            Amount = request.Amount,
            ValidFrom = now,
            ValidTo = null
        };

        _db.Set<ServicePrice>().Add(price);
        await _db.SaveChangesAsync(cancellationToken);

        // TODO (module securite) : tracer le changement de tarif dans AuditLogs avec request.ChangedByUserId.
        return ToDto(price);
    }

    public async Task<ServicePriceDto?> GetCurrentPriceAsync(
        int serviceId,
        BillingUnit unit,
        DateTime? at = null,
        CancellationToken cancellationToken = default)
    {
        var moment = at.HasValue
            ? InputValidation.ToUtc(at.Value)
            : _clock.GetUtcNow().UtcDateTime;

        var prices = await _db.Set<ServicePrice>()
            .AsNoTracking()
            .Where(p => p.ServiceId == serviceId && p.Unit == unit)
            .ToListAsync(cancellationToken);

        var current = prices
            .Where(p => p.ValidFrom <= moment && (p.ValidTo == null || moment < p.ValidTo))
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefault();

        return current is null ? null : ToDto(current);
    }

    private async Task EnsureNameIsAvailableAsync(string name, int? ignoredServiceId, CancellationToken cancellationToken)
    {
        // Comparaison faite en memoire : le catalogue est petit et SQLite est sensible a la casse.
        var existing = await _db.Set<Service>()
            .AsNoTracking()
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(cancellationToken);

        var duplicate = existing.Any(s =>
            s.Id != ignoredServiceId
            && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            throw new InvalidOperationException($"Un service nommé « {name} » existe déjà.");
        }
    }

    private static ServiceDto ToDto(Service service)
    {
        var prices = service.Prices
            .OrderBy(p => p.Unit)
            .ThenByDescending(p => p.ValidFrom)
            .Select(x => ToDto(x))
            .ToList();

        return new ServiceDto(service.Id, service.Name, service.Description, service.IsActive, prices);
    }

    private static ServicePriceDto ToDto(ServicePrice price) =>
        new(price.Id, price.ServiceId, price.Unit, price.Amount, price.ValidFrom, price.ValidTo);
}
