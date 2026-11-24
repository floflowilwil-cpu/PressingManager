using PressingManager.Core.Common;

namespace PressingManager.Tests.Unit;

public class FcfaFormatterTests
{
    [Theory]
    [InlineData(12500, "12 500 FCFA")]
    [InlineData(0, "0 FCFA")]
    [InlineData(950, "950 FCFA")]
    [InlineData(1234567, "1 234 567 FCFA")]
    [InlineData(-1500, "-1 500 FCFA")]
    public void FormatFcfa_GroupsThousandsAndAppendsTheCurrency(int amount, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.FormatFcfa(amount));
    }

    [Fact]
    public void FormatFcfa_RoundsToTheUnit_BecauseFcfaHasNoCents()
    {
        Assert.Equal("12 501 FCFA", MoneyFormatter.FormatFcfa(12500.5m));
        Assert.Equal("12 500 FCFA", MoneyFormatter.FormatFcfa(12500.4m));
    }

    [Fact]
    public void FormatFcfa_WithoutCurrency_ReturnsOnlyTheNumber()
    {
        Assert.Equal("3 000", MoneyFormatter.FormatFcfa(3000m, withCurrency: false));
    }
}
