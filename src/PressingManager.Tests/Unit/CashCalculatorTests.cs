using PressingManager.Core.Common;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;

namespace PressingManager.Tests.Unit;

public class CashCalculatorTests
{
    [Fact]
    public void GetExpectedAmount_ComputesOpeningPlusValidCashPaymentsMinusActiveCashExpenses()
    {
        var payments = new[]
        {
            new Payment { Amount = 15000m, Method = PaymentMethod.Cash, Status = PaymentStatus.Valid },
            new Payment { Amount = 5000m, Method = PaymentMethod.Cash, Status = PaymentStatus.Valid },
            new Payment { Amount = 8000m, Method = PaymentMethod.MtnMobileMoney, Status = PaymentStatus.Valid, Reference = "M1" },
            new Payment { Amount = 3000m, Method = PaymentMethod.Cash, Status = PaymentStatus.Cancelled },
            new Payment { Amount = 2000m, Method = PaymentMethod.Cash, Status = PaymentStatus.Refunded }
        };

        var expenses = new[]
        {
            new Expense { Amount = 4000m, Method = PaymentMethod.Cash, Category = "Detergent", IsCancelled = false },
            new Expense { Amount = 2500m, Method = PaymentMethod.OrangeMoney, Category = "Internet", IsCancelled = false },
            new Expense { Amount = 1500m, Method = PaymentMethod.Cash, Category = "Erreur", IsCancelled = true }
        };

        // 10000 (ouverture) + (15000 + 5000) - 4000 = 26000
        var expected = CashCalculator.GetExpectedAmount(10000m, payments, expenses);

        Assert.Equal(26000m, expected);
    }

    [Theory]
    [InlineData(26000, 26000, 0)]
    [InlineData(25500, 26000, -500)]
    [InlineData(26300, 26000, 300)]
    public void GetDiscrepancy_ReturnsCountedMinusExpected(double counted, double expected, double expectedDiscrepancy)
    {
        var discrepancy = CashCalculator.GetDiscrepancy((decimal)counted, (decimal)expected);

        Assert.Equal((decimal)expectedDiscrepancy, discrepancy);
    }
}
