using PressingManager.Core.Entities;

namespace PressingManager.Core.Interfaces;

/// <summary>Acces aux donnees des sessions de caisse (implementable en memoire ou via EF Core).</summary>
public interface ICashSessionRepository
{
    Task<CashSession?> GetByIdAsync(int cashSessionId, CancellationToken cancellationToken = default);

    Task<CashSession?> GetOpenSessionAsync(CancellationToken cancellationToken = default);

    Task AddAsync(CashSession session, CancellationToken cancellationToken = default);

    Task UpdateAsync(CashSession session, CancellationToken cancellationToken = default);
}
