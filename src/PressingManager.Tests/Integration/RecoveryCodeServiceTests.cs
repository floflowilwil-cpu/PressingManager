using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Tests.Unit;

namespace PressingManager.Tests.Integration;

public sealed class RecoveryCodeServiceTests : IDisposable
{
    private const string NewPassword = "Nouveau123";
    private const string GenericMessage = "Identifiant ou code de récupération incorrect.";

    private readonly AuthTestContext _ctx = new();

    public void Dispose() => _ctx.Dispose();

    /// <summary>Crée l'admin, génère son code, puis se déconnecte (comme un admin qui a oublié son mot de passe).</summary>
    private async Task<string> AdminWithCodeThenSignOutAsync()
    {
        var admin = await _ctx.SeedAdminAndSignInAsync();
        var code = await _ctx.Users.GenerateRecoveryCodeAsync(admin.Id);
        await _ctx.Auth.LogoutAsync();
        return code;
    }

    [Fact]
    public async Task GenerateRecoveryCode_RequiresManageUsers()
    {
        _ctx.SignInAs(UserRole.Cashier);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _ctx.Users.GenerateRecoveryCodeAsync(1));
    }

    [Fact]
    public async Task GenerateRecoveryCode_StoresOnlyAHash_AndNeverLogsTheCode()
    {
        var admin = await _ctx.SeedAdminAndSignInAsync();

        var code = await _ctx.Users.GenerateRecoveryCodeAsync(admin.Id);

        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$", code);

        await using var db = _ctx.Db.CreateDbContext();
        var stored = (await db.Users.SingleAsync()).RecoveryCodeHash;
        Assert.NotNull(stored);
        Assert.DoesNotContain(RecoveryCode.Normalize(code), stored);
        Assert.DoesNotContain(await db.AuditLogs.Select(a => a.Details).ToListAsync(), d => d != null && d.Contains(code));
        Assert.Contains(await db.AuditLogs.Select(a => a.Action).ToListAsync(), a => a == AuditActions.RecoveryCodeGenerated);
    }

    [Fact]
    public async Task ResetWithCode_ChangesThePassword()
    {
        var code = await AdminWithCodeThenSignOutAsync();

        await _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", code, NewPassword);

        Assert.True((await _ctx.Auth.LoginAsync("admin", NewPassword)).IsSuccess);
        Assert.False((await _ctx.Auth.LoginAsync("admin", AuthTestContext.AdminPassword)).IsSuccess);
    }

    [Fact]
    public async Task ResetWithCode_AcceptsLowercaseAndSpaces()
    {
        var code = await AdminWithCodeThenSignOutAsync();

        await _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("  ADMIN ", code.ToLowerInvariant().Replace('-', ' '), NewPassword);

        Assert.True((await _ctx.Auth.LoginAsync("admin", NewPassword)).IsSuccess);
    }

    [Fact]
    public async Task ResetWithCode_CodeCanBeUsedOnlyOnce()
    {
        var code = await AdminWithCodeThenSignOutAsync();
        await _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", code, NewPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", code, "Autre12345"));

        Assert.Equal(GenericMessage, ex.Message);
    }

    [Theory]
    [InlineData("admin", "AAAA-BBBB-CCCC")]   // bon identifiant, mauvais code
    [InlineData("inconnu", null)]               // identifiant inconnu, vrai code
    [InlineData("admin", "")]                   // code vide
    public async Task ResetWithCode_WrongInput_FailsWithTheSameGenericMessage(string username, string? code)
    {
        var realCode = await AdminWithCodeThenSignOutAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Auth.ResetPasswordWithRecoveryCodeAsync(username, code ?? realCode, NewPassword));

        Assert.Equal(GenericMessage, ex.Message);
    }

    [Fact]
    public async Task ResetWithCode_WeakPassword_IsRefused_AndKeepsTheCodeUsable()
    {
        var code = await AdminWithCodeThenSignOutAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", code, "faible"));

        await _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", code, NewPassword);
        Assert.True((await _ctx.Auth.LoginAsync("admin", NewPassword)).IsSuccess);
    }

    [Fact]
    public async Task ResetWithCode_AccountWithoutCode_IsRefused()
    {
        await _ctx.SeedAdminAndSignInAsync();
        await _ctx.Auth.LogoutAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", "AAAA-BBBB-CCCC", NewPassword));

        Assert.Equal(GenericMessage, ex.Message);
    }

    [Fact]
    public async Task ResetWithCode_TooManyWrongCodes_LocksTheAccountEvenForTheRightCode()
    {
        var code = await AdminWithCodeThenSignOutAsync();

        for (var i = 0; i < AuthService_MaxFailedAttempts; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", "AAAA-BBBB-CCCC", NewPassword));
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", code, NewPassword));

        // Après la durée de verrouillage, le bon code fonctionne de nouveau.
        _ctx.Time.Advance(TimeSpan.FromMinutes(6));
        await _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", code, NewPassword);

        Assert.True((await _ctx.Auth.LoginAsync("admin", NewPassword)).IsSuccess);
    }

    [Fact]
    public async Task ResetWithCode_DeactivatedAccount_IsRefused()
    {
        await _ctx.SeedAdminAndSignInAsync();
        var employee = await _ctx.Users.CreateAsync(new CreateUserDto("Awa Nkodo", "awa", "Secret123", UserRole.Cashier));
        var code = await _ctx.Users.GenerateRecoveryCodeAsync(employee.Id);
        await _ctx.Users.SetActiveAsync(employee.Id, false);
        await _ctx.Auth.LogoutAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("awa", code, NewPassword));

        Assert.Equal(GenericMessage, ex.Message);
    }

    [Fact]
    public async Task ResetWithCode_IsAuditedWithoutTheCode()
    {
        var code = await AdminWithCodeThenSignOutAsync();

        await _ctx.Auth.ResetPasswordWithRecoveryCodeAsync("admin", code, NewPassword);

        await using var db = _ctx.Db.CreateDbContext();
        var logs = await db.AuditLogs.ToListAsync();
        Assert.Contains(logs, l => l.Action == AuditActions.PasswordResetWithRecoveryCode);
        Assert.DoesNotContain(logs, l => (l.Details ?? "").Contains(code));
    }

    private const int AuthService_MaxFailedAttempts = PressingManager.Infrastructure.Services.AuthService.MaxFailedAttempts;
}
