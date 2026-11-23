using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Unit;

namespace PressingManager.Tests.Integration;

public sealed class AuthServiceTests : IDisposable
{
    private readonly AuthTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task IsSetupRequired_IsTrueOnlyWhileNoAccountExists()
    {
        Assert.True(await _ctx.Auth.IsSetupRequiredAsync());

        await _ctx.SeedAdminAndSignInAsync();

        Assert.False(await _ctx.Auth.IsSetupRequiredAsync());
    }

    [Fact]
    public async Task CreateInitialAdministrator_IsRefusedWhenAnAccountExists()
    {
        await _ctx.SeedAdminAndSignInAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Auth.CreateInitialAdministratorAsync("Autre", "autre", "Secret123"));
    }

    [Fact]
    public async Task CreateInitialAdministrator_RefusesWeakPassword()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Auth.CreateInitialAdministratorAsync("Admin", "admin", "faible"));
    }

    [Fact]
    public async Task Login_Success_SignsInAndRecordsLastLogin()
    {
        await _ctx.Auth.CreateInitialAdministratorAsync("Admin Test", "admin", AuthTestContext.AdminPassword);

        var result = await _ctx.Auth.LoginAsync("admin", AuthTestContext.AdminPassword);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserRole.Administrator, result.User!.Role);
        Assert.True(_ctx.Session.IsAuthenticated);
        Assert.NotNull(result.User.LastLoginAt);
    }

    [Fact]
    public async Task Login_UsernameIsCaseInsensitive()
    {
        await _ctx.Auth.CreateInitialAdministratorAsync("Admin Test", "admin", AuthTestContext.AdminPassword);

        var result = await _ctx.Auth.LoginAsync("  ADMIN ", AuthTestContext.AdminPassword);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("admin", "mauvais123")]
    [InlineData("inconnu", "Secret123")]
    public async Task Login_BadCredentials_FailWithTheSameGenericMessage(string username, string password)
    {
        await _ctx.Auth.CreateInitialAdministratorAsync("Admin Test", "admin", AuthTestContext.AdminPassword);

        var result = await _ctx.Auth.LoginAsync(username, password);

        Assert.False(result.IsSuccess);
        Assert.Equal("Nom d'utilisateur ou mot de passe incorrect.", result.ErrorMessage);
        Assert.False(_ctx.Session.IsAuthenticated);
    }

    [Fact]
    public async Task Login_LocksAccountAfterFiveFailures_ThenUnlocksAfterTheDelay()
    {
        await _ctx.Auth.CreateInitialAdministratorAsync("Admin Test", "admin", AuthTestContext.AdminPassword);

        for (var i = 0; i < AuthService.MaxFailedAttempts; i++)
        {
            await _ctx.Auth.LoginAsync("admin", "mauvais123");
        }

        // Meme avec le bon mot de passe, le compte est verrouille.
        var locked = await _ctx.Auth.LoginAsync("admin", AuthTestContext.AdminPassword);
        Assert.False(locked.IsSuccess);
        Assert.Contains("verrouillé", locked.ErrorMessage);

        _ctx.Time.Advance(AuthService.LockDuration + TimeSpan.FromSeconds(1));

        var unlocked = await _ctx.Auth.LoginAsync("admin", AuthTestContext.AdminPassword);
        Assert.True(unlocked.IsSuccess);
    }

    [Fact]
    public async Task Login_DeactivatedAccount_IsRefused()
    {
        await _ctx.SeedAdminAndSignInAsync();
        var cashier = await _ctx.Users.CreateAsync(
            new CreateUserDto("Caissier Test", "caissier", "Secret123", UserRole.Cashier));
        await _ctx.Users.SetActiveAsync(cashier.Id, false);
        await _ctx.Auth.LogoutAsync();

        var result = await _ctx.Auth.LoginAsync("caissier", "Secret123");

        Assert.False(result.IsSuccess);
        Assert.False(_ctx.Session.IsAuthenticated);
    }

    [Fact]
    public async Task Logout_ClearsTheSession()
    {
        await _ctx.SeedAdminAndSignInAsync();

        await _ctx.Auth.LogoutAsync();

        Assert.False(_ctx.Session.IsAuthenticated);
    }

    [Fact]
    public async Task SensitiveEvents_AreWrittenToTheAuditLog()
    {
        await _ctx.Auth.CreateInitialAdministratorAsync("Admin Test", "admin", AuthTestContext.AdminPassword);
        await _ctx.Auth.LoginAsync("admin", "mauvais123");
        await _ctx.Auth.LoginAsync("admin", AuthTestContext.AdminPassword);

        var actions = (await _ctx.Audit.GetRecentAsync(20)).Select(a => a.Action).ToList();

        Assert.Contains(AuditActions.InitialAdminCreated, actions);
        Assert.Contains(AuditActions.LoginFailed, actions);
        Assert.Contains(AuditActions.Login, actions);
    }

    [Fact]
    public async Task AuditLog_CanOnlyBeReadWithTheRightPermission()
    {
        _ctx.SignInAs(UserRole.Cashier);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _ctx.Audit.GetRecentAsync(10));
    }
}
