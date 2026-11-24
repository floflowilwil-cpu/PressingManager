using PressingManager.Core.Common;

namespace PressingManager.Tests.Unit;

public class MoneyFormatterTests
{
    [Theory]
    [InlineData(0, "0,00")]
    [InlineData(5, "5,00")]
    [InlineData(1250.5, "1 250,50")]
    [InlineData(1234567.89, "1 234 567,89")]
    [InlineData(-300, "-300,00")]
    public void Format_UsesSpaceThousandsAndCommaDecimals(double value, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.Format((decimal)value));
    }
}
