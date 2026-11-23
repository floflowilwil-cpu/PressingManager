using PressingManager.Core.Common;
using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

public sealed record UserDto(
    int Id,
    string FullName,
    string Username,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt)
{
    public string RoleLabel => UserRoleLabels.ToFrench(Role);

    /// <summary>Derniere connexion en heure locale (la base stocke l'UTC).</summary>
    public DateTime? LastLoginLocal =>
        LastLoginAt is { } utc ? DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime() : null;
}

public sealed record CreateUserDto(string FullName, string Username, string Password, UserRole Role);

public sealed record UpdateUserDto(int Id, string FullName, UserRole Role);

public sealed record LoginResult(bool IsSuccess, UserDto? User, string? ErrorMessage)
{
    public static LoginResult Success(UserDto user) => new(true, user, null);

    public static LoginResult Failure(string message) => new(false, null, message);
}

public sealed record AuditLogDto(
    int Id,
    int? UserId,
    string? UserName,
    string Action,
    string? EntityName,
    string? EntityId,
    string? Details,
    DateTime CreatedAt);
