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
        new(c.Id, c.CustomerNumber, c.FullName, c.Phone, c.Address, c.Notes, c.IsActive, c.CreatedAt);

    public static OrderDetailsDto ToOrderDto(Order o, string? customerName = null) =>
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
                .ToList());

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
}

public sealed class CustomerService : ICustomerService
{
    private const int MaxResults = 50;

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

    public async Task<IReadOnlyList<CustomerDto>> SearchAsync(
        string searchText, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageCustomers);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Customers.AsNoTracking().Where(c => c.IsActive);

        var text = (searchText ?? string.Empty).Trim();
        if (text.Length > 0)
        {
            // LIKE est insensible a la casse sous SQLite mais sensible sous PostgreSQL : on met les deux cotes en minuscules.
            var pattern = $"%{EscapeLike(text.ToLower())}%";
            query = query.Where(c =>
                EF.Functions.Like(c.FullName.ToLower(), pattern, "\\") ||
                EF.Functions.Like(c.Phone.ToLower(), pattern, "\\") ||
                EF.Functions.Like(c.CustomerNumber.ToLower(), pattern, "\\"));
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
        var address = CustomersOrdersMapping.Clean(request.Address, 300, "L'adresse");
        var notes = CustomersOrdersMapping.Clean(request.Notes, 1000, "Les notes");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var nameLower = name.ToLower();
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
            Address = address,
            Notes = notes,
            IsActive = true,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return CustomersOrdersMapping.ToCustomerDto(customer);
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

    // Les caractères % et _ ont un sens spécial dans une recherche LIKE : on les neutralise.
    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

public sealed class OrderService : IOrderService
{
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

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (customer is null || !customer.IsActive)
        {
            throw new BusinessRuleException("Client introuvable ou désactivé.");
        }

        // Préfixe du jour, ex. "CMD-20261123-" (on retire les 4 chiffres de la séquence).
        var datePrefix = NumberGenerator.OrderNumber(now.Date, 1);
        datePrefix = datePrefix.Substring(0, datePrefix.Length - 4);

        var sequence = await db.Orders.CountAsync(o => o.OrderNumber.StartsWith(datePrefix), cancellationToken) + 1;
        var orderNumber = NumberGenerator.OrderNumber(now.Date, sequence);
        while (await db.Orders.AnyAsync(o => o.OrderNumber == orderNumber, cancellationToken))
        {
            sequence++;
            orderNumber = NumberGenerator.OrderNumber(now.Date, sequence);
        }

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
            Items = items
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        return CustomersOrdersMapping.ToOrderDto(order, customer.FullName);
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

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}