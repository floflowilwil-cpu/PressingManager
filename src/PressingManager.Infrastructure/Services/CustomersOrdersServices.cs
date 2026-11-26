using System.Globalization;
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
    private const int MaxReasonLength = 300;

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

    // ---------- Création ----------

    public async Task<OrderDetailsDto> CreateAsync(
        CreateOrderDto request, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageOrders);

        var items = BuildItems(request.Items);
        var discount = Round(request.Discount);
        var fees = Round(request.Fees);
        ValidateAmounts(discount, fees, items.Sum(i => i.LineTotal));

        var now = _time.GetLocalNow();
        if (request.ExpectedDate is { } expected && expected.Date < now.Date)
        {
            throw new BusinessRuleException("La date prévue ne peut pas être dans le passé.");
        }

        var notes = CustomersOrdersMapping.Clean(request.Notes, 1000, "Les notes");
        var userId = CurrentUserId();

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
                // La commande et sa première ligne d'historique sont enregistrées ensemble :
                // si l'une échoue, l'autre est annulée (la transaction est fermée avant le catch).
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                db.Orders.Add(order);
                await db.SaveChangesAsync(cancellationToken);
                db.OrderStatusHistory.Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    FromStatus = null,
                    ToStatus = OrderStatus.Received,
                    ChangedByUserId = userId,
                    ChangedAt = now.UtcDateTime,
                    IsCorrection = false,
                    Comment = "Création de la commande"
                });

                await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return CustomersOrdersMapping.ToOrderDto(
                    order,
                    customer.FullName,
                    customer.Phone,
                    customer.Email);
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

    // ---------- Lecture et recherche ----------

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

    public async Task<OrderDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewOrders);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

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
        ValidatePeriod(filter);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var orders = await ApplyFilter(
                db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.Items),
                filter)
            .OrderByDescending(o => o.CreatedAt)
            .Take(MaxSearchResults)
            .ToListAsync(cancellationToken);

        return orders.Select(o => CustomersOrdersMapping.ToOrderDto(o)).ToList();
    }

    // ---------- Vue d'ensemble (cartes + tableau) ----------

    public async Task<OrderOverviewDto> GetOverviewAsync(
        OrderSearchDto filter, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewOrders);

        filter ??= new OrderSearchDto();
        ValidatePeriod(filter);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Toutes les commandes du filtre : les cartes comptent tout, le tableau n'en montre que 200.
        var orders = await ApplyFilter(
                db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.Items),
                filter)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        // Montant payé par commande (paiements valides seulement), lu par paquets pour les longues listes.
        var paidByOrder = new Dictionary<int, decimal>();
        foreach (var chunk in orders.Select(o => o.Id).Chunk(500))
        {
            var payments = await db.Payments.AsNoTracking()
                .Where(p => chunk.Contains(p.OrderId))
                .ToListAsync(cancellationToken);

            foreach (var group in payments.GroupBy(p => p.OrderId))
            {
                paidByOrder[group.Key] = PaymentRules.GetPaidAmount(group);
            }
        }

        var rows = new List<OrderOverviewRowDto>();
        int pending = 0, inProgress = 0, completed = 0, cancelled = 0;
        decimal billed = 0m, collected = 0m, remaining = 0m;

        foreach (var order in orders)
        {
            paidByOrder.TryGetValue(order.Id, out var paid);
            var total = order.Total;
            var isCancelled = order.Status == OrderStatus.Cancelled;
            var balance = isCancelled ? 0m : Math.Max(0m, total - paid);

            switch (order.Status)
            {
                case OrderStatus.Received or OrderStatus.AwaitingProcessing:
                    pending++;
                    break;
                case OrderStatus.Cleaning or OrderStatus.Ironing:
                    inProgress++;
                    break;
                case OrderStatus.ReadyForPickup or OrderStatus.Delivered:
                    completed++;
                    break;
                case OrderStatus.Cancelled:
                    cancelled++;
                    break;
            }

            // Une commande annulée ne compte ni dans le facturé, ni dans l'encaissé, ni dans le reste à percevoir.
            if (!isCancelled)
            {
                billed += total;
                collected += paid;
                remaining += balance;
            }

            if (rows.Count < MaxSearchResults)
            {
                rows.Add(new OrderOverviewRowDto(
                    order.Id,
                    order.OrderNumber,
                    order.CustomerId,
                    order.Customer?.FullName ?? string.Empty,
                    order.Customer?.Phone ?? string.Empty,
                    order.CreatedAt,
                    order.ExpectedDate,
                    order.Status,
                    order.Items.Sum(i => i.Quantity),
                    total,
                    paid,
                    balance));
            }
        }

        var stats = new OrderStatsDto(
            orders.Count, pending, inProgress, completed, cancelled, billed, collected, remaining);

        return new OrderOverviewDto(stats, rows);
    }

    // ---------- Modification ----------

    public async Task<OrderDetailsDto> UpdateAsync(
        UpdateOrderDto request, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageOrders);
        ArgumentNullException.ThrowIfNull(request);

        var items = BuildItems(request.Items);
        var discount = Round(request.Discount);
        var fees = Round(request.Fees);
        var subtotal = items.Sum(i => i.LineTotal);
        ValidateAmounts(discount, fees, subtotal);

        var notes = CustomersOrdersMapping.Clean(request.Notes, 1000, "Les notes");
        var userId = CurrentUserId();
        var now = _time.GetLocalNow();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new BusinessRuleException("Commande introuvable.");

        if (!OrderEditRules.CanEdit(order.Status))
        {
            throw new BusinessRuleException(
                "Une commande ne peut être modifiée que tant qu'elle est « Reçu » ou « En attente de traitement ».");
        }

        // On ne refuse une date passée que si l'utilisateur vient de la changer :
        // une commande déjà en retard doit rester modifiable.
        if (request.ExpectedDate is { } expected
            && expected.Date != order.ExpectedDate?.Date
            && expected.Date < now.Date)
        {
            throw new BusinessRuleException("La date prévue ne peut pas être dans le passé.");
        }

        // Les services déjà présents sur la commande restent acceptés même s'ils ont été désactivés depuis.
        var existingServiceIds = order.Items.Select(i => i.ServiceId).ToHashSet();
        var newServiceIds = items.Select(i => i.ServiceId).Distinct()
            .Where(id => !existingServiceIds.Contains(id))
            .ToList();
        if (newServiceIds.Count > 0)
        {
            var activeServices = await db.Services.AsNoTracking()
                .CountAsync(s => newServiceIds.Contains(s.Id) && s.IsActive, cancellationToken);
            if (activeServices != newServiceIds.Count)
            {
                throw new BusinessRuleException("Un des services sélectionnés est introuvable ou désactivé.");
            }
        }

        // Le nouveau total ne peut pas passer sous ce qui a déjà été encaissé.
        var newTotal = subtotal - discount + fees;
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.OrderId == order.Id)
            .ToListAsync(cancellationToken);
        var paid = PaymentRules.GetPaidAmount(payments);
        if (newTotal < paid)
        {
            throw new BusinessRuleException(
                $"Le nouveau total ({MoneyFormatter.FormatFcfa(newTotal)}) est inférieur au montant déjà payé " +
                $"({MoneyFormatter.FormatFcfa(paid)}). Remboursez ou annulez un paiement d'abord.");
        }

        var oldTotal = order.Total;

        db.RemoveRange(order.Items.ToList());
        order.Items = items;
        order.ExpectedDate = request.ExpectedDate;
        order.Discount = discount;
        order.Fees = fees;
        order.Notes = notes;

        if (newTotal != oldTotal)
        {
            var details = $"{order.OrderNumber} : total " +
                          $"{oldTotal.ToString("0.##", CultureInfo.InvariantCulture)} -> " +
                          $"{newTotal.ToString("0.##", CultureInfo.InvariantCulture)}";
            await AddAuditAsync(db, userId, AuditActions.PriceChanged, order, details, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return CustomersOrdersMapping.ToOrderDto(order, order.Customer?.FullName);
    }

    // ---------- Annulation ----------

    public async Task<OrderDetailsDto> CancelAsync(
        CancelOrderDto request, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageOrders);
        ArgumentNullException.ThrowIfNull(request);

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
        {
            throw new BusinessRuleException("Le motif d'annulation est obligatoire.");
        }

        if (reason.Length > MaxReasonLength)
        {
            throw new BusinessRuleException($"Le motif est trop long ({MaxReasonLength} caractères maximum).");
        }

        var userId = CurrentUserId();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new BusinessRuleException("Commande introuvable.");

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new BusinessRuleException("Cette commande est déjà annulée.");
        }

        if (!OrderEditRules.CanCancel(order.Status))
        {
            throw new BusinessRuleException("Une commande livrée ne peut plus être annulée.");
        }

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.OrderId == order.Id)
            .ToListAsync(cancellationToken);
        var paid = PaymentRules.GetPaidAmount(payments);
        if (paid > 0m)
        {
            throw new BusinessRuleException(
                $"Cette commande a déjà {MoneyFormatter.FormatFcfa(paid)} de paiements valides. " +
                "Remboursez-les d'abord dans Paiements, puis réessayez.");
        }

        var previous = order.Status;

        var entry = new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previous,
            ToStatus = OrderStatus.Cancelled,
            ChangedByUserId = userId,
            ChangedAt = _time.GetUtcNow().UtcDateTime,
            IsCorrection = false,
            Comment = reason
        };

        order.Status = OrderStatus.Cancelled;
        db.OrderStatusHistory.Add(entry);

        await AddAuditAsync(
            db,
            userId,
            AuditActions.OrderCancelled,
            order,
            $"{order.OrderNumber} : {previous} -> {OrderStatus.Cancelled} ({reason})",
            cancellationToken);

        // Statut, historique et audit sont enregistrés ensemble.
        await db.SaveChangesAsync(cancellationToken);

        return CustomersOrdersMapping.ToOrderDto(order, order.Customer?.FullName);
    }

    // ---------- Outils internes ----------

    private int CurrentUserId() =>
        _session.CurrentUser?.Id ?? throw new BusinessRuleException("Aucun utilisateur connecté.");

    private static void ValidatePeriod(OrderSearchDto filter)
    {
        if (filter.FromDate is { } from && filter.ToDate is { } to && from.Date > to.Date)
        {
            throw new BusinessRuleException("La date de début ne peut pas être après la date de fin.");
        }
    }

    /// <summary>Filtre commun à la recherche et à la vue d'ensemble : statut, période et texte.</summary>
    private IQueryable<Order> ApplyFilter(IQueryable<Order> query, OrderSearchDto filter)
    {
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

        return query;
    }

    private async Task AddAuditAsync(
        PressingDbContext db,
        int userId,
        string action,
        Order order,
        string details,
        CancellationToken cancellationToken)
    {
        // Le journal d'audit exige un utilisateur connu de la base : sinon on garde la trace sans auteur.
        var userExists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);

        db.AuditLogs.Add(new AuditLog
        {
            UserId = userExists ? userId : null,
            Action = action,
            EntityName = nameof(Order),
            EntityId = order.Id.ToString(CultureInfo.InvariantCulture),
            Details = details.Length > 1000 ? details[..1000] : details,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        });
    }

    /// <summary>Valide les lignes saisies et construit les lignes de commande (création et modification).</summary>
    private static List<OrderItem> BuildItems(IReadOnlyList<CreateOrderItemDto>? lines)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new BusinessRuleException("Une commande doit contenir au moins un article.");
        }

        var items = new List<OrderItem>();
        foreach (var line in lines)
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

        return items;
    }

    private static void ValidateAmounts(decimal discount, decimal fees, decimal subtotal)
    {
        if (discount < 0)
        {
            throw new BusinessRuleException("La remise ne peut pas être négative.");
        }

        if (fees < 0)
        {
            throw new BusinessRuleException("Les frais ne peuvent pas être négatifs.");
        }

        if (discount > subtotal + fees)
        {
            throw new BusinessRuleException("La remise ne peut pas dépasser le montant de la commande.");
        }
    }

    private async Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Orders.AnyAsync(o => o.OrderNumber == orderNumber, cancellationToken);
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}