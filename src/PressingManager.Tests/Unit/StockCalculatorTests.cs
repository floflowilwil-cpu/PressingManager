using PressingManager.Core.Common;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;

namespace PressingManager.Tests.Unit;

public class StockCalculatorTests
{
    private static StockMovement Move(StockMovementType type, decimal quantity) =>
        new() { Type = type, Quantity = quantity };

    [Fact]
    public void GetCurrentStock_SumsSignedMovements()
    {
        var movements = new[]
        {
            Move(StockMovementType.Entry, 10m),
            Move(StockMovementType.Exit, 3.5m),
            Move(StockMovementType.AdjustmentIncrease, 1m),
            Move(StockMovementType.AdjustmentDecrease, 0.5m)
        };

        Assert.Equal(7m, StockCalculator.GetCurrentStock(movements));
    }

    [Fact]
    public void GetCurrentStock_NoMovement_IsZero()
    {
        Assert.Equal(0m, StockCalculator.GetCurrentStock(Array.Empty<StockMovement>()));
    }

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, false)]
    public void IsBelowAlertThreshold_ComparesToThreshold(double stock, double threshold, bool expected)
    {
        Assert.Equal(expected, StockCalculator.IsBelowAlertThreshold((decimal)stock, (decimal)threshold));
    }

    [Theory]
    [InlineData(10, 4, true)]
    [InlineData(10, 10, true)]
    [InlineData(10, 11, false)]
    [InlineData(10, 0, false)]
    [InlineData(10, -1, false)]
    public void CanRemove_RefusesNegativeStockAndNonPositiveQuantity(double stock, double quantity, bool expected)
    {
        Assert.Equal(expected, StockCalculator.CanRemove((decimal)stock, (decimal)quantity));
    }
}
