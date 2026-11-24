using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;

namespace PressingManager.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

    private const string InvalidCredentials = "Nom d'utilisateur ou mot de passe incorrect.";
    private const string InvalidRecovery = "Identifiant ou code de récupération incorrect.";

    private readonly IDbContextFactory<PressingDbContext> _dbFactory;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUserContext _session;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public AuthService(
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

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return !await db.Users.AnyAsync(cancellationToken);
    }

    public async Task<UserDto> CreateInitialAdministratorAsync(
        string fullName, string username, string password, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            throw new BusinessRuleException("Un compte existe déjà : la configuration initiale n'est plus possible.");
        }

        var error = UserRules.ValidateNewUser(fullName, username, password);
        if (error is not null)
        {
            throw new BusinessRuleException(error);
        }

        var user = new User
        {
            FullName = fullName.Trim(),
            Username = UserRules.NormalizeUsername(username),
            PasswordHash = _hasher.Hash(password),
            Role = UserRole.Administrator,
            IsActive = true,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            AuditActions.InitialAdminCreated, nameof(User), user.Id.ToString(), user.Username,
            user.Id, cancellationToken);

        return UserMapping.ToDto(user);
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var normalized = UserRules.NormalizeUsername(username);
        var now = _time.GetUtcNow().UtcDateTime;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == normalized, cancellationToken);

        if (user is null)
        {
            await _audit.LogAsync(
                AuditActions.LoginFailed, nameof(User), details: $"Utilisateur inconnu : {normalized}",
                cancellationToken: cancellationToken);
            return LoginResult.Failure(InvalidCredentials);
        }

        if (!user.IsActive)
        {
            await _audit.LogAsync(
                AuditActions.LoginFailed, nameof(User), user.Id.ToString(), "Compte désactivé",
                user.Id, cancellationToken);
            return LoginResult.Failure("Ce compte est désactivé. Contactez un administrateur.");
        }

        if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            var minutes = (int)Math.Ceiling((lockedUntil - now).TotalMinutes);
            return LoginResult.Failure($"Compte verrouillé après trop d'échecs. Réessayez dans {minutes} minute(s).");
        }

        if (!_hasher.Verify(password, user.PasswordHash))
        {
            return await RegisterFailureAsync(db, user, now, cancellationToken);
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        await db.SaveChangesAsync(cancellationToken);

        var dto = UserMapping.ToDto(user);
        _session.SignIn(dto);

        await _audit.LogAsync(
            AuditActions.Login, nameof(User), user.Id.ToString(), userId: user.Id,
            cancellationToken: cancellationToken);

        return LoginResult.Success(dto);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_session.CurrentUser is { } current)
        {
            await _audit.LogAsync(
                AuditActions.Logout, nameof(User), current.Id.ToString(), userId: current.Id,
                cancellationToken: cancellationToken);
        }

        _session.SignOut();
    }

    public async Task ResetPasswordWithRecoveryCodeAsync(
        string username, string recoveryCode, string newPassword, CancellationToken cancellationToken = default)
    {
        var policyError = PasswordPolicy.Validate(newPassword);
        if (policyError is not null)
        {
            throw new BusinessRuleException(policyError);
        }

        var normalized = UserRules.NormalizeUsername(username);
        var code = RecoveryCode.Normalize(recoveryCode);
        var now = _time.GetUtcNow().UtcDateTime;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == normalized, cancellationToken);

        // Même message dans tous les cas : on ne révèle pas si le compte existe, est désactivé ou n'a pas de code.
        if (user is null || !user.IsActive || user.RecoveryCodeHash is null)
        {
            await _audit.LogAsync(
                AuditActions.RecoveryFailed, nameof(User), user?.Id.ToString(), "Récupération refusée",
                user?.Id, cancellationToken);
            throw new BusinessRuleException(InvalidRecovery);
        }

        if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            var minutes = (int)Math.Ceiling((lockedUntil - now).TotalMinutes);
            throw new BusinessRuleException($"Compte verrouillé après trop d'échecs. Réessayez dans {minutes} minute(s).");
        }

        if (code.Length == 0 || !_hasher.Verify(code, user.RecoveryCodeHash))
        {
            await RegisterRecoveryFailureAsync(db, user, now, cancellationToken);
            throw new BusinessRuleException(InvalidRecovery);
        }

        user.PasswordHash = _hasher.Hash(newPassword);
        user.RecoveryCodeHash = null; // usage unique : l'administrateur doit en générer un nouveau
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            AuditActions.PasswordResetWithRecoveryCode, nameof(User), user.Id.ToString(), user.Username,
            user.Id, cancellationToken);
    }

    /// <summary>Un mauvais code compte comme un échec de connexion (même verrouillage de 5 minutes).</summary>
    private async Task RegisterRecoveryFailureAsync(
        PressingDbContext db, User user, DateTime now, CancellationToken cancellationToken)
    {
        user.FailedLoginAttempts++;
        var locked = user.FailedLoginAttempts >= MaxFailedAttempts;

        if (locked)
        {
            user.LockedUntil = now + LockDuration;
            user.FailedLoginAttempts = 0;
        }

        await db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            locked ? AuditActions.AccountLocked : AuditActions.RecoveryFailed,
            nameof(User), user.Id.ToString(), userId: user.Id, cancellationToken: cancellationToken);
    }

    private async Task<LoginResult> RegisterFailureAsync(
        PressingDbContext db, User user, DateTime now, CancellationToken cancellationToken)
    {
        user.FailedLoginAttempts++;
        var locked = user.FailedLoginAttempts >= MaxFailedAttempts;

        if (locked)
        {
            user.LockedUntil = now + LockDuration;
            user.FailedLoginAttempts = 0;
        }

        await db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(
            locked ? AuditActions.AccountLocked : AuditActions.LoginFailed,
            nameof(User), user.Id.ToString(), userId: user.Id, cancellationToken: cancellationToken);

        return LoginResult.Failure(locked
            ? $"Trop d'échecs : compte verrouillé pendant {(int)LockDuration.TotalMinutes} minutes."
            : InvalidCredentials);
    }
}
