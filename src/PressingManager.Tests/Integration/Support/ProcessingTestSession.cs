using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;

namespace PressingManager.Tests.Integration.Support;

/// <summary>Session de test : un utilisateur connecte avec le role voulu (administrateur par defaut).</summary>
public static class ProcessingTestSession
{
    public static CurrentUserContext SignedIn(UserRole role = UserRole.Administrator, int userId = 1)
    {
        var session = new CurrentUserContext();
        session.SignIn(new UserDto(userId, "Utilisateur test", $"test{userId}", role, true, null));
        return session;
    }

    /// <summary>Session sans utilisateur connecte.</summary>
    public static CurrentUserContext SignedOut() => new();
}
