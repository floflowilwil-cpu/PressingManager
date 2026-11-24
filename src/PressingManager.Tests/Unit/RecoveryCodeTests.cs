using PressingManager.Core.Common;

namespace PressingManager.Tests.Unit;

public class RecoveryCodeTests
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    [Fact]
    public void Generate_HasThreeGroupsOfFourAllowedCharacters()
    {
        var code = RecoveryCode.Generate();

        var groups = code.Split('-');
        Assert.Equal(3, groups.Length);
        Assert.All(groups, g => Assert.Equal(4, g.Length));
        Assert.All(code.Replace("-", ""), c => Assert.Contains(c, Alphabet));
    }

    [Fact]
    public void Generate_GivesDifferentCodesEachTime()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => RecoveryCode.Generate()).ToHashSet();

        Assert.Equal(200, codes.Count);
    }

    [Theory]
    [InlineData("k7qm-2xwd-9prt", "K7QM2XWD9PRT")]
    [InlineData("  K7QM 2XWD 9PRT ", "K7QM2XWD9PRT")]
    [InlineData("K7QM2XWD9PRT", "K7QM2XWD9PRT")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_IgnoresCaseSpacesAndDashes(string? input, string expected)
    {
        Assert.Equal(expected, RecoveryCode.Normalize(input));
    }

    [Fact]
    public void Normalize_OfAGeneratedCode_RemovesOnlyTheDashes()
    {
        var code = RecoveryCode.Generate();

        Assert.Equal(code.Replace("-", ""), RecoveryCode.Normalize(code));
    }
}
