using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Services;

internal static class UserMapping
{
    public static UserDto ToDto(User user) =>
        new(user.Id, user.FullName, user.Username, user.Role, user.IsActive, user.LastLoginAt);
}

public sealed class UserService : IUserService
{
    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUserContext _session;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public UserService(
        IDbContextFactory<PressingDbContext> dbFactory,
        IPasswordHasher hasher,
        ICurrentUserContext session,
        IAuditService audit,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _hasher = hasher;
        _session = session;
        _audit = audit;
        _time = time;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageUsers);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var users = await db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(cancellationToken);
        return users.Select(UserMapping.ToDto).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageUsers);

        var error = UserRules.ValidateNewUser(request.FullName, request.Username, request.Password);
        if (error is not null)
        {
            throw new BusinessRuleException(error);
        }

        var username = UserRules.NormalizeUsername(request.Username);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.Users.AnyAsync(u => u.Username == username, cancellationToken))
        {
            throw new BusinessRuleException("Ce nom d'utilisateur est déjà utilisé.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Username = username,
            PasswordHash = _hasher.Hash(request.Password),
            Role = request.Role,
            IsActive = true,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            AuditActions.UserCreated, nameof(User), user.Id.ToString(), $"{user.Username} ({user.Role})",
            cancellationToken: cancellationToken);

        return UserMapping.ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(UpdateUserDto request, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageUsers);

        var error = UserRules.ValidateFullName(request.FullName);
        if (error is not null)
        {
            throw new BusinessRuleException(error);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await LoadAsync(db, request.Id, cancellationToken);

        if (user.Role == UserRole.Administrator && request.Role != UserRole.Administrator && user.IsActive)
        {
            await EnsureAnotherActiveAdministratorAsync(db, user.Id, cancellationToken);
        }

        var details = $"{user.Role} -> {request.Role}";
        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        await db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            AuditActions.UserUpdated, nameof(User), user.Id.ToString(), details,
            cancellationToken: cancellationToken);

        return UserMapping.ToDto(user);
    }

    public async Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageUsers);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await LoadAsync(db, userId, cancellationToken);

        if (user.IsActive == isActive)
        {
            return;
        }

        if (!isActive)
        {
            if (_session.CurrentUser?.Id == user.Id)
            {
                throw new BusinessRuleException("Vous ne pouvez pas désactiver votre propre compte.");
            }

            if (user.Role == UserRole.Administrator)
            {
                await EnsureAnotherActiveAdministratorAsync(db, user.Id, cancellationToken);
            }
        }

        user.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            isActive ? AuditActions.UserActivated : AuditActions.UserDeactivated,
            nameof(User), user.Id.ToString(), user.Username, cancellationToken: cancellationToken);
    }

    public async Task ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageUsers);

        var error = PasswordPolicy.Validate(newPassword);
        if (error is not null)
        {
            throw new BusinessRuleException(error);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await LoadAsync(db, userId, cancellationToken);

        user.PasswordHash = _hasher.Hash(newPassword);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            AuditActions.PasswordReset, nameof(User), user.Id.ToString(), user.Username,
            cancellationToken: cancellationToken);
    }

    public async Task<string> GenerateRecoveryCodeAsync(int userId, CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ManageUsers);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await LoadAsync(db, userId, cancellationToken);

        var code = RecoveryCode.Generate();
        user.RecoveryCodeHash = _hasher.Hash(RecoveryCode.Normalize(code));
        await db.SaveChangesAsync(cancellationToken);

        // Le code n'est jamais écrit dans le journal d'audit.
        await _audit.LogAsync(
            AuditActions.RecoveryCodeGenerated, nameof(User), user.Id.ToString(), user.Username,
            cancellationToken: cancellationToken);

        return code;
    }

    public async Task ChangeOwnPasswordAsync(
        string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var current = _session.CurrentUser
                      ?? throw new PermissionDeniedException("Aucun utilisateur connecté.");

        var error = PasswordPolicy.Validate(newPassword);
        if (error is not null)
        {
            throw new BusinessRuleException(error);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await LoadAsync(db, current.Id, cancellationToken);

        if (!_hasher.Verify(currentPassword, user.PasswordHash))
        {
            throw new BusinessRuleException("Le mot de passe actuel est incorrect.");
        }

        user.PasswordHash = _hasher.Hash(newPassword);
        await db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            AuditActions.PasswordChanged, nameof(User), user.Id.ToString(), cancellationToken: cancellationToken);
    }

    private static async Task<User> LoadAsync(PressingDbContext db, int userId, CancellationToken cancellationToken) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new BusinessRuleException("Utilisateur introuvable.");

    private static async Task EnsureAnotherActiveAdministratorAsync(
        PressingDbContext db, int excludedUserId, CancellationToken cancellationToken)
    {
        var otherAdminExists = await db.Users.AnyAsync(
            u => u.Id != excludedUserId && u.IsActive && u.Role == UserRole.Administrator, cancellationToken);

        if (!otherAdminExists)
        {
            throw new BusinessRuleException("Il doit toujours rester au moins un administrateur actif.");
        }
    }
}
