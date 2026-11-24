using PressingManager.Core.Common;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;

namespace PressingManager.Tests.Unit;

public class RolePermissionsTests
{
    [Fact]
    public void Administrator_HasEveryPermission()
    {
        foreach (var permission in Enum.GetValues<Permission>())
        {
            Assert.True(RolePermissions.Has(UserRole.Administrator, permission));
        }
    }

    [Theory]
    [InlineData(UserRole.FrontDesk, Permission.ManageOrders, true)]
    [InlineData(UserRole.FrontDesk, Permission.RegisterPayments, false)]
    [InlineData(UserRole.Cashier, Permission.RegisterPayments, true)]
    [InlineData(UserRole.Cashier, Permission.ManageUsers, false)]
    [InlineData(UserRole.Cashier, Permission.RefundPayments, false)]
    [InlineData(UserRole.Processor, Permission.UpdateOrderStatus, true)]
    [InlineData(UserRole.Processor, Permission.CorrectOrderStatus, false)]
    [InlineData(UserRole.StockManager, Permission.ManageStock, true)]
    [InlineData(UserRole.StockManager, Permission.ViewReports, false)]
    public void Has_FollowsTheRoleMatrix(UserRole role, Permission permission, bool expected)
    {
        Assert.Equal(expected, RolePermissions.Has(role, permission));
    }

    [Fact]
    public void OnlyAdministrator_CanManageUsersAndViewAudit()
    {
        foreach (var role in Enum.GetValues<UserRole>().Where(r => r != UserRole.Administrator))
        {
            Assert.False(RolePermissions.Has(role, Permission.ManageUsers));
            Assert.False(RolePermissions.Has(role, Permission.ViewAuditLog));
        }
    }
}

public class UserRulesTests
{
    [Theory]
    [InlineData("abc12345")]
    [InlineData("MotDePasse9")]
    [InlineData("12345678a")]
    public void PasswordPolicy_AcceptsValidPasswords(string password)
    {
        Assert.Null(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("court1")]
    [InlineData("seulementdeslettres")]
    [InlineData("1234567890")]
    public void PasswordPolicy_RefusesWeakPasswords(string? password)
    {
        Assert.NotNull(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData("  Admin ", "admin")]
    [InlineData("JEAN.Dupont", "jean.dupont")]
    public void NormalizeUsername_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, UserRules.NormalizeUsername(input));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("jean dupont")]
    [InlineData("jean@dupont")]
    [InlineData("")]
    public void ValidateUsername_RefusesInvalidNames(string username)
    {
        Assert.NotNull(UserRules.ValidateUsername(username));
    }

    [Fact]
    public void ValidateNewUser_ReturnsNullWhenEverythingIsValid()
    {
        Assert.Null(UserRules.ValidateNewUser("Jean Dupont", "jean.dupont", "Secret123"));
    }

    [Fact]
    public void ValidateNewUser_RefusesEmptyFullName()
    {
        Assert.NotNull(UserRules.ValidateNewUser("  ", "jean", "Secret123"));
    }
}

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void Hash_ThenVerify_Succeeds_AndHashIsNotThePassword()
    {
        var hash = _hasher.Hash("Secret123");

        Assert.DoesNotContain("Secret123", hash);
        Assert.True(_hasher.Verify("Secret123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        Assert.False(_hasher.Verify("Autre456", _hasher.Hash("Secret123")));
    }

    [Fact]
    public void Hash_UsesARandomSalt()
    {
        Assert.NotEqual(_hasher.Hash("Secret123"), _hasher.Hash("Secret123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pas-un-hash")]
    [InlineData("PBKDF2-SHA256$abc$xx$yy")]
    [InlineData("PBKDF2-SHA256$1000$!!!$???")]
    public void Verify_MalformedHash_ReturnsFalseWithoutThrowing(string hash)
    {
        Assert.False(_hasher.Verify("Secret123", hash));
    }

    [Fact]
    public void Verify_StillWorksWhenIterationsChangeLater()
    {
        var oldHash = new Pbkdf2PasswordHasher(iterations: 1_000).Hash("Secret123");

        Assert.True(new Pbkdf2PasswordHasher(iterations: 2_000).Verify("Secret123", oldHash));
    }
}
