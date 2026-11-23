using PressingManager.Core.Entities;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Repositories;

/// <summary>
/// Implementation en memoire de <see cref="IExpenseRepository"/>.
/// Utilisable pour les tests et le prototypage en attendant PressingDbContext (EF Core / SQLite).
/// </summary>
public sealed class InMemoryExpenseRepository : IExpenseRepository
{
    private readonly List<Expense> _expenses = new();
    private readonly Lock _sync = new();
    private int _nextId = 1;

    public Task<Expense?> GetByIdAsync(int expenseId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_expenses.FirstOrDefault(e => e.Id == expenseId));
        }
    }

    public Task<IReadOnlyList<Expense>> GetByCashSessionIdAsync(int cashSessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            IReadOnlyList<Expense> result = _expenses
                .Where(e => e.CashSessionId == cashSessionId)
                .OrderBy(e => e.Date)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<Expense>> SearchAsync(
        DateTime from,
        DateTime to,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            IEnumerable<Expense> query = _expenses
                .Where(e => e.Date >= from && e.Date <= to);

            if (!string.IsNullOrWhiteSpace(category))
            {
                var normalizedCategory = category.Trim();
                query = query.Where(e =>
                    string.Equals(e.Category.Trim(), normalizedCategory, StringComparison.OrdinalIgnoreCase));
            }

            IReadOnlyList<Expense> result = query
                .OrderByDescending(e => e.Date)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task AddAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(expense);

        lock (_sync)
        {
            if (expense.Id == 0)
            {
                expense.Id = _nextId++;
            }

            _expenses.Add(expense);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(expense);

        lock (_sync)
        {
            var index = _expenses.FindIndex(e => e.Id == expense.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"Depense #{expense.Id} introuvable.");
            }

            _expenses[index] = expense;
        }

        return Task.CompletedTask;
    }
}
