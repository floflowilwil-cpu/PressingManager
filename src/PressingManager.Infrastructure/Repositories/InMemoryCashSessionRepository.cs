using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Repositories;

/// <summary>
/// Implementation en memoire de <see cref="ICashSessionRepository"/>.
/// Utilisable pour les tests et le prototypage en attendant PressingDbContext (EF Core / SQLite).
/// </summary>
public sealed class InMemoryCashSessionRepository : ICashSessionRepository
{
    private readonly List<CashSession> _sessions = new();
    private readonly Lock _sync = new();
    private int _nextId = 1;

    public Task<CashSession?> GetByIdAsync(int cashSessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_sessions.FirstOrDefault(s => s.Id == cashSessionId));
        }
    }

    public Task<CashSession?> GetOpenSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_sessions.FirstOrDefault(s => s.Status == CashSessionStatus.Open));
        }
    }

    public Task AddAsync(CashSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        lock (_sync)
        {
            if (session.Id == 0)
            {
                session.Id = _nextId++;
            }

            _sessions.Add(session);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(CashSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        lock (_sync)
        {
            var index = _sessions.FindIndex(s => s.Id == session.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"Session de caisse #{session.Id} introuvable.");
            }

            _sessions[index] = session;
        }

        return Task.CompletedTask;
    }
}
