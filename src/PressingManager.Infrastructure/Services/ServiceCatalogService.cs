using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Catalogue des prestations et de leurs tarifs (documentation, section 4.5).
/// Meme schema que les autres modules : un IDbContextFactory (un contexte court par operation)
/// et des erreurs metier de type BusinessRuleException.
/// </summary>
public sealed class ServiceCatalogService : IServiceCatalogService
{
    /// <summary>Action ecrite dans le journal d'audit a chaque changement de tarif.</summary>
    public const string PriceChangedAuditAction = "ServicePriceChanged";

    private const int NameMaxLength = 100;
    private const int DescriptionMaxLength = 500;

    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly ICurrentUserContext _session;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public ServiceCatalogService(
        IDbContextFactory<PressingDbContext> dbFactory,
        ICurrentUserContext session,
        IAuditService audit,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _session = session;
        _audit = audit;
        _time = time;
    }

    public async Task<IReadOnlyList<ServiceDto>> GetServicesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Service> query = db.Services.AsNoTracking().Include(s => s.Prices);

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var services = await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);
        return services.Select(s => ToDto(s)).ToList();
    }

    public async Task<ServiceDto?> GetByIdAsync(
        int serviceId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var service = await db.Services
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
        _session.Require(Permission.ManageServices);

        var name = InputValidation.RequireText(request.Name, "Le nom du service", NameMaxLength);
        var description = InputValidation.OptionalText(request.Description, "La description", DescriptionMaxLength);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureNameIsAvailableAsync(db, name, ignoredServiceId: null, cancellationToken);

        var service = new Service
        {
            Name = name,
            Description = description,
            IsActive = true
        };

        db.Services.Add(service);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(service);
    }

    public async Task<ServiceDto> UpdateAsync(
        UpdateServiceDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session.Require(Permission.ManageServices);

        var name = InputValidation.RequireText(request.Name, "Le nom du service", NameMaxLength);
        var description = InputValidation.OptionalText(request.Description, "La description", DescriptionMaxLength);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var service = await db.Services
            .Include(s => s.Prices)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new BusinessRuleException("Service introuvable.");

        await EnsureNameIsAvailableAsync(db, name, service.Id, cancellationToken);

        service.Name = name;
        service.Description = description;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(service);
    }

    public async Task SetActiveAsync(
        int serviceId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageServices);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var service = await db.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken)
            ?? throw new BusinessRuleException("Service introuvable.");

        if (service.IsActive == isActive)
        {
            return;
        }

        service.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ServicePriceDto> SetPriceAsync(
        SetServicePriceDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session.Require(Permission.ManageServices);

        if (request.Amount <= 0)
        {
            throw new BusinessRuleException("Le tarif doit être strictement positif.");
        }

        if (!Enum.IsDefined(request.Unit))
        {
            throw new BusinessRuleException("L'unité de facturation est inconnue.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var service = await db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ServiceId, cancellationToken)
            ?? throw new BusinessRuleException("Service introuvable.");

        var userIsValid = await db.Users
            .AnyAsync(u => u.Id == request.ChangedByUserId && u.IsActive, cancellationToken);
        if (!userIsValid)
        {
            throw new BusinessRuleException("Utilisateur introuvable ou désactivé.");
        }

        var now = _time.GetUtcNow().UtcDateTime;

        // Tarifs encore ouverts (ValidTo = null) pour ce service et cette unite.
        var openPrices = await db.ServicePrices
            .Where(p => p.ServiceId == request.ServiceId && p.Unit == request.Unit && p.ValidTo == null)
            .ToListAsync(cancellationToken);

        // Meme montant que le tarif en vigueur : rien a changer, l'historique reste propre.
        var unchanged = openPrices.FirstOrDefault(p => p.Amount == request.Amount && p.ValidFrom <= now);
        if (unchanged is not null)
        {
            return ToDto(unchanged);
        }

        var previousAmount = openPrices
            .OrderByDescending(p => p.ValidFrom)
            .Select(p => (decimal?)p.Amount)
            .FirstOrDefault();

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

        db.ServicePrices.Add(price);
        await db.SaveChangesAsync(cancellationToken);

        var before = previousAmount is null
            ? "aucun"
            : previousAmount.Value.ToString("0.##", CultureInfo.InvariantCulture);
        var after = request.Amount.ToString("0.##", CultureInfo.InvariantCulture);

        await _audit.LogAsync(
            PriceChangedAuditAction,
            nameof(ServicePrice),
            price.Id.ToString(CultureInfo.InvariantCulture),
            $"{service.Name} ({request.Unit}) : {before} -> {after}",
            request.ChangedByUserId,
            cancellationToken);

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
            : _time.GetUtcNow().UtcDateTime;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var prices = await db.ServicePrices
            .AsNoTracking()
            .Where(p => p.ServiceId == serviceId && p.Unit == unit)
            .ToListAsync(cancellationToken);

        var current = prices
            .Where(p => p.ValidFrom <= moment && (p.ValidTo == null || moment < p.ValidTo))
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefault();

        return current is null ? null : ToDto(current);
    }

    private static async Task EnsureNameIsAvailableAsync(
        PressingDbContext db,
        string name,
        int? ignoredServiceId,
        CancellationToken cancellationToken)
    {
        // Comparaison en memoire : le catalogue est petit et SQLite est sensible a la casse.
        var existing = await db.Services
            .AsNoTracking()
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(cancellationToken);

        var duplicate = existing.Any(s =>
            s.Id != ignoredServiceId
            && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            throw new BusinessRuleException($"Un service nommé « {name} » existe déjà.");
        }
    }

    private static ServiceDto ToDto(Service service)
    {
        var prices = service.Prices
            .OrderBy(p => p.Unit)
            .ThenByDescending(p => p.ValidFrom)
            .Select(p => ToDto(p))
            .ToList();

        return new ServiceDto(service.Id, service.Name, service.Description, service.IsActive, prices);
    }

    private static ServicePriceDto ToDto(ServicePrice price) =>
        new(price.Id, price.ServiceId, price.Unit, price.Amount, price.ValidFrom, price.ValidTo);
}
