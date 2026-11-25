using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Services;

internal static class CustomersOrdersMapping
{
    public static CustomerDto ToCustomerDto(Customer c) =>
        new(c.Id, c.CustomerNumber, c.FullName, c.Phone, c.Address, c.Notes, c.IsActive, c.CreatedAt, c.Email);

    public static OrderDetailsDto ToOrderDto(
        Order o,
        string? customerName = null,
        string? customerPhone = null,
        string? customerEmail = null) =>
        new(
            o.Id,
            o.OrderNumber,
            o.CustomerId,
            customerName ?? o.Customer?.FullName ?? string.Empty,
            o.CreatedAt,
            o.ExpectedDate,
            o.Status,
            o.Subtotal,
            o.Discount,
            o.Fees,
            o.Total,
            o.Notes,
            o.Items
                .Select(i => new OrderItemDto(
                    i.Id, i.ServiceId, i.ItemType, i.Quantity, i.UnitPrice, i.LineTotal, i.Observations))
                .ToList(),
            customerPhone ?? o.Customer?.Phone,
            customerEmail ?? o.Customer?.Email);

    /// <summary>Supprime les espaces ; renvoie null si vide ; refuse un texte trop long.</summary>
    public static string? Clean(string? value, int maxLength, string label)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length > maxLength)
        {
            throw new BusinessRuleException($"{label} est trop long ({maxLength} caractères maximum).");
        }

        return text;
    }

    public static string? CleanEmail(string? value)
    {
        var email = Clean(value, 200, "L'adresse e-mail");
        if (email is null)
        {
            return null;
        }

        var at = email.IndexOf('@');
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed)
            || !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase)
            || at <= 0
            || email.IndexOf('.', at) <= at + 1
            || email.EndsWith('.'))
        {
            throw new BusinessRuleException("L'adresse e-mail du client est invalide.");
        }

        return email;
    }

    // Les caractères % et _ ont un sens spécial dans une recherche LIKE : on les neutralise.
    public static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

public sealed class CustomerService : ICustomerService
{
    private const int MaxResults = 50;

    // Nombre d'essais si deux postes prennent le même numéro en même temps.
    private const int MaxNumberAttempts = 5;

    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly ICurrentUserContext _session;
    private readonly TimeProvider _time;

    public CustomerService(
        IDbContextFactory<PressingDbContext> dbFactory,
        ICurrentUserContext session,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _session = session;
        _time = time;
    }

    public Task<IReadOnlyList<CustomerDto>> SearchAsync(
        string searchText, CancellationToken cancellationToken = default) =>
        SearchByStateAsync(searchText, isActive: true, cancellationToken);

    public Task<IReadOnlyList<CustomerDto>> SearchInactiveAsync(
        string searchText, CancellationToken cancellationToken = default) =>
        SearchByStateAsync(searchText, isActive: false, cancellationToken);

    private async Task<IReadOnlyList<CustomerDto>> SearchByStateAsync(
        string searchText, bool isActive, CancellationToken cancellationToken)
    {
        _session.Require(Permission.ManageCustomers);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Customers.AsNoTracking().Where(c => c.IsActive == isActive);

        var text = (searchText ?? string.Empty).Trim();
        if (text.Length > 0)
        {
            // LIKE est insensible a la casse sous SQLite mais sensible sous PostgreSQL : on met les deux cotes en minuscules.
            var pattern = $"%{CustomersOrdersMapping.EscapeLike(text.ToLower())}%";
            query = query.Where(c =>
                EF.Functions.Like(c.FullName.ToLower(), pattern, "\\") ||
                EF.Functions.Like(c.Phone.ToLower(), pattern, "\\") ||
                EF.Functions.Like(c.CustomerNumber.ToLower(), pattern, "\\") ||
                (c.Email != null && EF.Functions.Like(c.Email.ToLower(), pattern, "\\")));
        }

        var customers = await query
            .OrderBy(c => c.FullName)
            .Take(MaxResults)
            .ToListAsync(cancellationToken);

        return customers.Select(c => CustomersOrdersMapping.ToCustomerDto(c)).ToList();
    }

    public async Task<CustomerDto> CreateAsync(
        CreateCustomerDto request, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageCustomers);

        var (name, phone) = Validate(request.FullName, request.Phone);
        var email = CustomersOrdersMapping.CleanEmail(request.Email);
        var address = CustomersOrdersMapping.Clean(request.Address, 300, "L'adresse");
        var notes = CustomersOrdersMapping.Clean(request.Notes, 1000, "Les notes");
        var nameLower = name.ToLower();

        for (var attempt = 1; ; attempt++)
        {
            // Un contexte neuf à chaque essai : après un échec, l'ancien contexte n'est plus fiable.
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            if (await db.Customers.AnyAsync(
                    c => c.IsActive && c.Phone == phone && c.FullName.ToLower() == nameLower,
                    cancellationToken))
            {
                throw new BusinessRuleException("Ce client existe déjà (même nom et même téléphone).");
            }

            var sequence = await db.Customers.CountAsync(cancellationToken) + 1;
            var number = NumberGenerator.CustomerNumber(sequence);
            while (await db.Customers.AnyAsync(c => c.CustomerNumber == number, cancellationToken))
            {
                sequence++;
                number = NumberGenerator.CustomerNumber(sequence);
            }

            var customer = new Customer
            {
                CustomerNumber = number,
                FullName = name,
                Phone = phone,
                Email = email,
                Address = address,
                Notes = notes,
                IsActive = true,
                CreatedAt = _time.GetUtcNow().UtcDateTime
            };

            try
            {
                db.Customers.Add(customer);
                await db.SaveChangesAsync(cancellationToken);
                return CustomersOrdersMapping.ToCustomerDto(customer);
            }
            catch (DbUpdateException)
            {
                // Si ce numéro existe maintenant en base, un autre poste l'a pris avant nous : on recommence.
                // Sinon c'est une autre erreur : on la laisse remonter.
                if (!await CustomerNumberExistsAsync(number, cancellationToken))
                {
                    throw;
                }

                if (attempt >= MaxNumberAttempts)
                {
                    throw new BusinessRuleException(
                        "Impossible d'attribuer un numéro de client pour le moment. Veuillez réessayer.");
                }
            }
        }
    }

    public async Task<CustomerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageCustomers);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return customer is null ? null : CustomersOrdersMapping.ToCustomerDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(
        UpdateCustomerDto request, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageCustomers);

        var (name, phone) = Validate(request.FullName, request.Phone);
        var email = CustomersOrdersMapping.CleanEmail(request.Email);
        var address = CustomersOrdersMapping.Clean(request.Address, 300, "L'adresse");
        var notes = CustomersOrdersMapping.Clean(request.Notes, 1000, "Les notes");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new BusinessRuleException("Client introuvable.");

        var nameLower = name.ToLower();
        if (await db.Customers.AnyAsync(
                c => c.Id != request.Id && c.IsActive && c.Phone == phone && c.FullName.ToLower() == nameLower,
                cancellationToken))
        {
            throw new BusinessRuleException("Un autre client a déjà le même nom et le même téléphone.");
        }

        customer.FullName = name;
        customer.Phone = phone;
        customer.Email = email;
        customer.Address = address;
        customer.Notes = notes;

        await db.SaveChangesAsync(cancellationToken);

        return CustomersOrdersMapping.ToCustomerDto(customer);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageCustomers);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new BusinessRuleException("Client introuvable.");

        if (!customer.IsActive)
        {
            return;
        }

        customer.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerDto> ReactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageCustomers);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new BusinessRuleException("Client introuvable.");

        if (customer.IsActive)
        {
            return CustomersOrdersMapping.ToCustomerDto(customer);
        }

        // Même règle que pour la création : pas deux clients actifs identiques.
        var nameLower = customer.FullName.ToLower();
        var phone = customer.Phone;
        if (await db.Customers.AnyAsync(
                c => c.Id != id && c.IsActive && c.Phone == phone && c.FullName.ToLower() == nameLower,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "Impossible de réactiver : un client actif a déjà le même nom et le même téléphone.");
        }

        customer.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);

        return CustomersOrdersMapping.ToCustomerDto(customer);
    }

    private async Task<bool> CustomerNumberExistsAsync(string number, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Customers.AnyAsync(c => c.CustomerNumber == number, cancellationToken);
    }

    private static (string Name, string Phone) Validate(string? fullName, string? phone)
    {
        var name = (fullName ?? string.Empty).Trim();
        var number = (phone ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            throw new BusinessRuleException("Le nom du client est obligatoire.");
        }

        if (name.Length > 150)
        {
            throw new BusinessRuleException("Le nom du client est trop long (150 caractères maximum).");
        }

        if (number.Length == 0)
        {
            throw new BusinessRuleException("Le téléphone du client est obligatoire.");
        }

        if (number.Length > 30)
        {
            throw new BusinessRuleException("Le téléphone est trop long (30 caractères maximum).");
        }

        return (name, number);
    }
}

public sealed class OrderService : IOrderService
{
    private const int MaxSearchResults = 200;

    // Nombre d'essais si deux postes prennent le même numéro en même temps.
    private const int MaxNumberAttempts = 5;

    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly ICurrentUserContext _session;
    private readonly TimeProvider _time;

    public OrderService(
        IDbContextFactory<PressingDbContext> dbFactory,
        ICurrentUserContext session,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _session = session;
        _time = time;
    }

    public async Task<OrderDetailsDto> CreateAsync(
        CreateOrderDto request, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageOrders);

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessRuleException("Une commande doit contenir au moins un article.");
        }

        var items = new List<OrderItem>();
        foreach (var line in request.Items)
        {
            var itemType = (line.ItemType ?? string.Empty).Trim();
            if (itemType.Length == 0)
            {
                throw new BusinessRuleException("Chaque article doit avoir un type (ex. chemise).");
            }

            if (itemType.Length > 100)
            {
                throw new BusinessRuleException("Le type d'article est trop long (100 caractères maximum).");
            }

            if (line.Quantity < 1)
            {
                throw new BusinessRuleException("La quantité d'un article doit être d'au moins 1.");
            }

            if (line.UnitPrice < 0)
            {
                throw new BusinessRuleException("Le prix d'un article ne peut pas être négatif.");
            }

            items.Add(new OrderItem
            {
                ServiceId = line.ServiceId,
                ItemType = itemType,
                Quantity = line.Quantity,
                UnitPrice = Round(line.UnitPrice),
                Observations = CustomersOrdersMapping.Clean(line.Observations, 500, "L'observation")
            });
        }

        var discount = Round(request.Discount);
        var fees = Round(request.Fees);

        if (discount < 0)
        {
            throw new BusinessRuleException("La remise ne peut pas être négative.");
        }

        if (fees < 0)
        {
            throw new BusinessRuleException("Les frais ne peuvent pas être négatifs.");
        }

        var subtotal = items.Sum(i => i.LineTotal);
        if (discount > subtotal + fees)
        {
            throw new BusinessRuleException("La remise ne peut pas dépasser le montant de la commande.");
        }

        var now = _time.GetLocalNow();
        if (request.ExpectedDate is { } expected && expected.Date < now.Date)
        {
            throw new BusinessRuleException("La date prévue ne peut pas être dans le passé.");
        }

        var notes = CustomersOrdersMapping.Clean(request.Notes, 1000, "Les notes");

        // Préfixe du jour, ex. "CMD-20261123-" (on retire les 4 chiffres de la séquence).
        var datePrefix = NumberGenerator.OrderNumber(now.Date, 1);
        datePrefix = datePrefix.Substring(0, datePrefix.Length - 4);

        for (var attempt = 1; ; attempt++)
        {
            // Un contexte neuf à chaque essai : après un échec, l'ancien contexte n'est plus fiable.
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var customer = await db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
            if (customer is null || !customer.IsActive)
            {
                throw new BusinessRuleException("Client introuvable ou désactivé.");
            }

            // Chaque ligne doit référencer un service actif du catalogue (clé étrangère OrderItems -> Services).
            var serviceIds = items.Select(i => i.ServiceId).Distinct().ToList();
            var activeServices = await db.Services.AsNoTracking()
                .CountAsync(s => serviceIds.Contains(s.Id) && s.IsActive, cancellationToken);
            if (activeServices != serviceIds.Count)
            {
                throw new BusinessRuleException("Un des services sélectionnés est introuvable ou désactivé.");
            }

            var sequence = await db.Orders.CountAsync(o => o.OrderNumber.StartsWith(datePrefix), cancellationToken) + 1;
            var orderNumber = NumberGenerator.OrderNumber(now.Date, sequence);
            while (await db.Orders.AnyAsync(o => o.OrderNumber == orderNumber, cancellationToken))
            {
                sequence++;
                orderNumber = NumberGenerator.OrderNumber(now.Date, sequence);
            }

            // Nouvelles lignes à chaque essai (on ne réutilise pas des objets déjà passés dans un autre contexte).
            var lines = items
                .Select(i => new OrderItem
                {
                    ServiceId = i.ServiceId,
                    ItemType = i.ItemType,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Observations = i.Observations
                })
                .ToList();

            var order = new Order
            {
                OrderNumber = orderNumber,
                CustomerId = customer.Id,
                CreatedAt = now.UtcDateTime,
                ExpectedDate = request.ExpectedDate,
                Status = OrderStatus.Received,
                Discount = discount,
                Fees = fees,
                Notes = notes,
                Items = lines
            };

            try
            {
                db.Orders.Add(order);
                await db.SaveChangesAsync(cancellationToken);
                return CustomersOrdersMapping.ToOrderDto(order, customer.FullName, customer.Phone, customer.Email);
            }
            catch (DbUpdateException)
            {
                // Si ce numéro existe maintenant en base, un autre poste l'a pris avant nous : on recommence.
                // Sinon c'est une autre erreur : on la laisse remonter.
                if (!await OrderNumberExistsAsync(orderNumber, cancellationToken))
                {
                    throw;
                }

                if (attempt >= MaxNumberAttempts)
                {
                    throw new BusinessRuleException(
                        "Impossible d'attribuer un numéro de commande pour le moment. Veuillez réessayer.");
                }
            }
        }
    }

    public async Task<OrderDetailsDto?> GetByNumberAsync(
        string orderNumber, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewOrders);

        var number = (orderNumber ?? string.Empty).Trim().ToUpperInvariant();
        if (number.Length == 0)
        {
            return null;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == number, cancellationToken);

        return order is null ? null : CustomersOrdersMapping.ToOrderDto(order);
    }

    public async Task<IReadOnlyList<OrderDetailsDto>> GetByCustomerAsync(
        int customerId, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewOrders);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(o => CustomersOrdersMapping.ToOrderDto(o)).ToList();
    }

    public async Task<IReadOnlyList<OrderDetailsDto>> SearchAsync(
        OrderSearchDto filter, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewOrders);

        filter ??= new OrderSearchDto();

        if (filter.FromDate is { } from && filter.ToDate is { } to && from.Date > to.Date)
        {
            throw new BusinessRuleException("La date de début ne peut pas être après la date de fin.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .AsQueryable();

        if (filter.Status is { } status)
        {
            query = query.Where(o => o.Status == status);
        }

        // Les dates de création sont stockées en UTC : on convertit les jours choisis (heure locale) en UTC.
        // Fin de période = début du lendemain, exclu, pour inclure toute la journée choisie.
        var timeZone = _time.LocalTimeZone;

        if (filter.FromDate is { } fromDate)
        {
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Unspecified), timeZone);
            query = query.Where(o => o.CreatedAt >= startUtc);
        }

        if (filter.ToDate is { } toDate)
        {
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Unspecified), timeZone);
            query = query.Where(o => o.CreatedAt < endUtc);
        }

        var text = (filter.SearchText ?? string.Empty).Trim();
        if (text.Length > 0)
        {
            var pattern = $"%{CustomersOrdersMapping.EscapeLike(text.ToLower())}%";
            query = query.Where(o =>
                EF.Functions.Like(o.OrderNumber.ToLower(), pattern, "\\") ||
                EF.Functions.Like(o.Customer!.FullName.ToLower(), pattern, "\\") ||
                EF.Functions.Like(o.Customer!.Phone.ToLower(), pattern, "\\"));
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(MaxSearchResults)
            .ToListAsync(cancellationToken);

        return orders.Select(o => CustomersOrdersMapping.ToOrderDto(o)).ToList();
    }

    private async Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Orders.AnyAsync(o => o.OrderNumber == orderNumber, cancellationToken);
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}