using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Interfaces;

/// <summary>Hachage des mots de passe (jamais de stockage en clair).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>
/// Session de l'utilisateur connecte. Les services metier des autres modules l'utilisent :
/// <c>session.Require(Permission.RegisterPayments)</c> et <c>session.CurrentUser!.Id</c>.
/// </summary>
public interface ICurrentUserContext
{
    UserDto? CurrentUser { get; }

    bool IsAuthenticated { get; }

    event EventHandler? SessionChanged;

    void SignIn(UserDto user);

    void SignOut();

    bool HasPermission(Permission permission);

    /// <summary>Leve <see cref="Common.PermissionDeniedException"/> si l'action n'est pas autorisee.</summary>
    void Require(Permission permission);
}

public interface IAuthService
{
    /// <summary>True tant qu'aucun compte n'existe (premier lancement).</summary>
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);

    /// <summary>Cree le premier administrateur ; refuse si un compte existe deja.</summary>
    Task<UserDto> CreateInitialAdministratorAsync(
        string fullName, string username, string password, CancellationToken cancellationToken = default);

    Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// « Mot de passe oublié » sans e-mail (application hors ligne) : l'utilisateur saisit son identifiant,
    /// le code de récupération remis par l'administrateur et un nouveau mot de passe.
    /// Le code est à usage unique. Lève BusinessRuleException (même message si l'identifiant ou le code est faux).
    /// </summary>
    Task ResetPasswordWithRecoveryCodeAsync(
        string username, string recoveryCode, string newPassword, CancellationToken cancellationToken = default);
}

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateAsync(UpdateUserDto request, CancellationToken cancellationToken = default);

    Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Reinitialisation par un administrateur (deverrouille aussi le compte).</summary>
    Task ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Génère un nouveau code de récupération (permission ManageUsers) et remplace l'ancien.
    /// Le code en clair n'est retourné qu'ici, une seule fois : seul son hash est stocké.
    /// </summary>
    Task<string> GenerateRecoveryCodeAsync(int userId, CancellationToken cancellationToken = default);

    Task ChangeOwnPasswordAsync(
        string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    /// <summary>
    /// Ajoute une trace. <paramref name="userId"/> = null : l'utilisateur connecte (s'il y en a un).
    /// </summary>
    Task LogAsync(
        string action,
        string? entityName = null,
        string? entityId = null,
        string? details = null,
        int? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Dernieres entrees, les plus recentes d'abord (permission ViewAuditLog).</summary>
    Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
