using Microsoft.EntityFrameworkCore;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Repositories;

/// <summary>Implementation EF Core / SQLite de <see cref="IPaymentRepository"/>.</summary>
public sealed class EfPaymentRepository : IPaymentRepository
{
    private readonly IDbContextFactory<PressingDbContext> _dbFactory;

    public EfPaymentRepository(IDbContextFactory<PressingDbContext> dbFactory)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    }

    public async Task<Payment?> GetByIdAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Payments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.PaidAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByCashSessionIdAsync(int cashSessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Payments
            .AsNoTracking()
            .Where(p => p.CashSessionId == cashSessionId)
            .OrderBy(p => p.PaidAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsValidReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var normalized = reference.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var references = await db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Valid && p.Reference != null)
            .Select(p => p.Reference!)
            .ToListAsync(cancellationToken);

        return references.Any(r => string.Equals(r.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Payments.Update(payment);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Implementation EF Core / SQLite de <see cref="IExpenseRepository"/>.</summary>
public sealed class EfExpenseRepository : IExpenseRepository
{
    private readonly IDbContextFactory<PressingDbContext> _dbFactory;

    public EfExpenseRepository(IDbContextFactory<PressingDbContext> dbFactory)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    }

    public async Task<Expense?> GetByIdAsync(int expenseId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId, cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByCashSessionIdAsync(int cashSessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Expenses
            .AsNoTracking()
            .Where(e => e.CashSessionId == cashSessionId)
            .OrderBy(e => e.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> SearchAsync(
        DateTime from,
        DateTime to,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var expenses = await db.Expenses
            .AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= to)
            .ToListAsync(cancellationToken);

        IEnumerable<Expense> query = expenses;
        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim();
            query = query.Where(e =>
                string.Equals(e.Category.Trim(), normalizedCategory, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderByDescending(e => e.Date)
            .ToList();
    }

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Expenses.Update(expense);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Implementation EF Core / SQLite de <see cref="ICashSessionRepository"/>.</summary>
public sealed class EfCashSessionRepository : ICashSessionRepository
{
    private readonly IDbContextFactory<PressingDbContext> _dbFactory;

    public EfCashSessionRepository(IDbContextFactory<PressingDbContext> dbFactory)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    }

    public async Task<CashSession?> GetByIdAsync(int cashSessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CashSessions.FirstOrDefaultAsync(c => c.Id == cashSessionId, cancellationToken);
    }

    public async Task<CashSession?> GetOpenSessionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CashSessions.FirstOrDefaultAsync(c => c.Status == CashSessionStatus.Open, cancellationToken);
    }

    public async Task AddAsync(CashSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.CashSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CashSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.CashSessions.Update(session);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Lit les informations de facturation des commandes depuis SQLite (avec lignes incluses).</summary>
public sealed class EfOrderBillingInfoProvider : IOrderBillingInfoProvider
{
    private readonly IDbContextFactory<PressingDbContext> _dbFactory;

    public EfOrderBillingInfoProvider(IDbContextFactory<PressingDbContext> dbFactory)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    }

    public async Task<OrderBillingInfoDto?> GetOrderBillingInfoAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        return order is null
            ? null
            : new OrderBillingInfoDto(order.Id, order.Total, order.Status == OrderStatus.Cancelled);
    }
}
