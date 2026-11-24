using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Tests.Unit;

namespace PressingManager.Tests.Integration;

public sealed class UserServiceTests : IDisposable
{
    private readonly AuthTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task EveryOperation_RequiresAnAuthenticatedUser()
    {
        await Assert.ThrowsAsync<PermissionDeniedException>(() => _ctx.Users.GetAllAsync());
    }

    [Theory]
    [InlineData(UserRole.FrontDesk)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Processor)]
    [InlineData(UserRole.StockManager)]
    public async Task NonAdministrators_CannotManageUsers(UserRole role)
    {
        _ctx.SignInAs(role);

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            _ctx.Users.CreateAsync(new CreateUserDto("X Y", "xy123", "Secret123", UserRole.Cashier)));
    }

    [Fact]
    public async Task Create_StoresAHashedPassword_AndTheNewUserCanLogin()
    {
        await _ctx.SeedAdminAndSignInAsync();

        await _ctx.Users.CreateAsync(new CreateUserDto("Marie Claire", "Marie", "Secret123", UserRole.FrontDesk));

        await using (var db = _ctx.Db.CreateDbContext())
        {
            var stored = await db.Users.SingleAsync(u => u.Username == "marie");
            Assert.NotEqual("Secret123", stored.PasswordHash);
            Assert.StartsWith("PBKDF2-SHA256$", stored.PasswordHash);
        }

        await _ctx.Auth.LogoutAsync();
        Assert.True((await _ctx.Auth.LoginAsync("marie", "Secret123")).IsSuccess);
    }

    [Fact]
    public async Task Create_DuplicateUsername_IsRefusedIgnoringCase()
    {
        await _ctx.SeedAdminAndSignInAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Users.CreateAsync(new CreateUserDto("Autre Admin", "ADMIN", "Secret123", UserRole.Cashier)));
    }

    [Fact]
    public async Task Create_WeakPassword_IsRefused()
    {
        await _ctx.SeedAdminAndSignInAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Users.CreateAsync(new CreateUserDto("Jean Dupont", "jean", "123", UserRole.Cashier)));
    }

    [Fact]
    public async Task Update_CannotDemoteTheLastActiveAdministrator()
    {
        var admin = await _ctx.SeedAdminAndSignInAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Users.UpdateAsync(new UpdateUserDto(admin.Id, "Admin Test", UserRole.Cashier)));
    }

    [Fact]
    public async Task Update_CanDemoteAnAdministratorWhenAnotherOneRemains()
    {
        var admin = await _ctx.SeedAdminAndSignInAsync();
        await _ctx.Users.CreateAsync(new CreateUserDto("Second Admin", "admin2", "Secret123", UserRole.Administrator));

        var updated = await _ctx.Users.UpdateAsync(new UpdateUserDto(admin.Id, "Admin Test", UserRole.Cashier));

        Assert.Equal(UserRole.Cashier, updated.Role);
    }

    [Fact]
    public async Task SetActive_CannotDeactivateYourOwnAccount()
    {
        var admin = await _ctx.SeedAdminAndSignInAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _ctx.Users.SetActiveAsync(admin.Id, false));
    }

    [Fact]
    public async Task ResetPassword_UnlocksTheAccountAndTheNewPasswordWorks()
    {
        await _ctx.SeedAdminAndSignInAsync();
        var user = await _ctx.Users.CreateAsync(new CreateUserDto("Marie Claire", "marie", "Secret123", UserRole.Cashier));

        for (var i = 0; i < 5; i++)
        {
            await _ctx.Auth.LoginAsync("marie", "mauvais123");
        }

        await _ctx.Users.ResetPasswordAsync(user.Id, "Nouveau456");

        Assert.True((await _ctx.Auth.LoginAsync("marie", "Nouveau456")).IsSuccess);
    }

    [Fact]
    public async Task ChangeOwnPassword_RequiresTheCurrentPassword()
    {
        await _ctx.SeedAdminAndSignInAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Users.ChangeOwnPasswordAsync("mauvais123", "Nouveau456"));

        await _ctx.Users.ChangeOwnPasswordAsync(AuthTestContext.AdminPassword, "Nouveau456");
        await _ctx.Auth.LogoutAsync();

        Assert.True((await _ctx.Auth.LoginAsync("admin", "Nouveau456")).IsSuccess);
    }

    [Fact]
    public async Task UserManagement_IsRecordedInTheAuditLog()
    {
        await _ctx.SeedAdminAndSignInAsync();
        var user = await _ctx.Users.CreateAsync(new CreateUserDto("Marie Claire", "marie", "Secret123", UserRole.Cashier));
        await _ctx.Users.SetActiveAsync(user.Id, false);

        var actions = (await _ctx.Audit.GetRecentAsync(20)).Select(a => a.Action).ToList();

        Assert.Contains(AuditActions.UserCreated, actions);
        Assert.Contains(AuditActions.UserDeactivated, actions);
    }
}
