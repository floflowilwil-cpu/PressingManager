using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>Session en memoire de l'utilisateur connecte (une seule session par application).</summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    private volatile UserDto? _currentUser;

    public UserDto? CurrentUser => _currentUser;

    public bool IsAuthenticated => _currentUser is not null;

    public event EventHandler? SessionChanged;

    public void SignIn(UserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _currentUser = user;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        _currentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool HasPermission(Permission permission) =>
        _currentUser is { IsActive: true } user && RolePermissions.Has(user.Role, permission);

    public void Require(Permission permission)
    {
        if (_currentUser is null)
        {
            throw new PermissionDeniedException("Aucun utilisateur connecté.");
        }

        if (!HasPermission(permission))
        {
            throw new PermissionDeniedException("Vous n'avez pas l'autorisation d'effectuer cette action.");
        }
    }
}
