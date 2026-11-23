using Microsoft.EntityFrameworkCore;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private const int MaxDetailsLength = 1000;

    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly ICurrentUserContext _session;
    private readonly TimeProvider _time;

    public AuditService(IDbContextFactory<PressingDbContext> dbFactory, ICurrentUserContext session, TimeProvider time)
    {
        _dbFactory = dbFactory;
        _session = session;
        _time = time;
    }

    public async Task LogAsync(
        string action,
        string? entityName = null,
        string? entityId = null,
        string? details = null,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? _session.CurrentUser?.Id,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details is { Length: > MaxDetailsLength } ? details[..MaxDetailsLength] : details,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ViewAuditLog);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(Math.Clamp(count, 1, 1000))
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserId,
                a.User != null ? a.User.FullName : null,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.Details,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
