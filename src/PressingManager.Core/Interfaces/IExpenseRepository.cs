using PressingManager.Core.Entities;

namespace PressingManager.Core.Interfaces;

/// <summary>Acces aux donnees des depenses (implementable en memoire ou via EF Core).</summary>
public interface IExpenseRepository
{
    Task<Expense?> GetByIdAsync(int expenseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Expense>> GetByCashSessionIdAsync(int cashSessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Expense>> SearchAsync(
        DateTime from,
        DateTime to,
        string? category = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Expense expense, CancellationToken cancellationToken = default);

    Task UpdateAsync(Expense expense, CancellationToken cancellationToken = default);
}
