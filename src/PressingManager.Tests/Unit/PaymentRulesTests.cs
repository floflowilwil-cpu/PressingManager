using PressingManager.Core.Common;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;

namespace PressingManager.Tests.Unit;

public class PaymentRulesTests
{
    private static Payment MakePayment(
        decimal amount,
        PaymentMethod method = PaymentMethod.Cash,
        PaymentStatus status = PaymentStatus.Valid,
        string? reference = null) =>
        new()
        {
            OrderId = 1,
            Amount = amount,
            Method = method,
            Status = status,
            Reference = reference,
            PaidAt = DateTime.UtcNow,
            ReceivedByUserId = 1
        };

    [Fact]
    public void GetRemainingBalance_SubtractsOnlyValidPayments()
    {
        var payments = new[]
        {
            MakePayment(2000m, PaymentMethod.Cash, PaymentStatus.Valid),
            MakePayment(1500m, PaymentMethod.MtnMobileMoney, PaymentStatus.Valid, "MTN-001"),
            MakePayment(1000m, PaymentMethod.Cash, PaymentStatus.Cancelled),
            MakePayment(500m, PaymentMethod.OrangeMoney, PaymentStatus.Refunded, "OM-002")
        };

        var paid = PaymentRules.GetPaidAmount(payments);
        var remaining = PaymentRules.GetRemainingBalance(5000m, payments);

        Assert.Equal(3500m, paid);
        Assert.Equal(1500m, remaining);
    }

    [Fact]
    public void ValidatePayment_RefusesOverpayment()
    {
        var existing = new[] { MakePayment(3000m) };

        Assert.Throws<InvalidOperationException>(() =>
            PaymentRules.ValidatePayment(
                orderTotal: 5000m,
                existingPayments: existing,
                amount: 2500m,
                method: PaymentMethod.Cash,
                reference: null,
                hasOpenCashSession: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void ValidatePayment_RefusesNonPositiveAmount(double amount)
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentRules.ValidatePayment(
                orderTotal: 5000m,
                existingPayments: Array.Empty<Payment>(),
                amount: (decimal)amount,
                method: PaymentMethod.Cash,
                reference: null,
                hasOpenCashSession: true));
    }

    [Theory]
    [InlineData(PaymentMethod.MtnMobileMoney)]
    [InlineData(PaymentMethod.OrangeMoney)]
    public void ValidatePayment_MobileMoney_RequiresNonEmptyReference(PaymentMethod method)
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentRules.ValidatePayment(
                orderTotal: 5000m,
                existingPayments: Array.Empty<Payment>(),
                amount: 2000m,
                method: method,
                reference: "   ",
                hasOpenCashSession: false));
    }

    [Fact]
    public void ValidatePayment_MobileMoney_RefusesDuplicateReference()
    {
        var existing = new[]
        {
            MakePayment(2000m, PaymentMethod.MtnMobileMoney, PaymentStatus.Valid, "TX-12345")
        };

        Assert.Throws<InvalidOperationException>(() =>
            PaymentRules.ValidatePayment(
                orderTotal: 5000m,
                existingPayments: existing,
                amount: 1000m,
                method: PaymentMethod.MtnMobileMoney,
                reference: " tx-12345 ",
                hasOpenCashSession: false));
    }

    [Fact]
    public void ValidatePayment_MobileMoney_AllowsReferenceWhenPreviousWasCancelled()
    {
        var existing = new[]
        {
            MakePayment(2000m, PaymentMethod.OrangeMoney, PaymentStatus.Cancelled, "OM-999")
        };

        var exception = Record.Exception(() =>
            PaymentRules.ValidatePayment(
                orderTotal: 5000m,
                existingPayments: existing,
                amount: 2000m,
                method: PaymentMethod.OrangeMoney,
                reference: "OM-999",
                hasOpenCashSession: false));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidatePayment_Cash_RefusesWhenNoCashSessionIsOpen()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentRules.ValidatePayment(
                orderTotal: 5000m,
                existingPayments: Array.Empty<Payment>(),
                amount: 2000m,
                method: PaymentMethod.Cash,
                reference: null,
                hasOpenCashSession: false));
    }

    [Fact]
    public void ValidatePayment_Cash_SucceedsWhenCashSessionIsOpenAndAmountWithinBalance()
    {
        var existing = new[] { MakePayment(2000m, PaymentMethod.Cash, PaymentStatus.Valid) };

        var exception = Record.Exception(() =>
            PaymentRules.ValidatePayment(
                orderTotal: 5000m,
                existingPayments: existing,
                amount: 3000m,
                method: PaymentMethod.Cash,
                reference: null,
                hasOpenCashSession: true));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCancellationOrRefund_RefusesMissingReason(string? reason)
    {
        var payment = MakePayment(1000m);

        Assert.Throws<InvalidOperationException>(() =>
            PaymentRules.ValidateCancellationOrRefund(payment, reason));
    }

    [Theory]
    [InlineData(PaymentStatus.Cancelled)]
    [InlineData(PaymentStatus.Refunded)]
    public void ValidateCancellationOrRefund_RefusesAlreadyCancelledOrRefundedPayment(PaymentStatus status)
    {
        var payment = MakePayment(1000m, status: status);

        Assert.Throws<InvalidOperationException>(() =>
            PaymentRules.ValidateCancellationOrRefund(payment, "Erreur de saisie"));
    }

    [Fact]
    public void ValidateCancellationOrRefund_SucceedsForValidPaymentWithReason()
    {
        var payment = MakePayment(1000m);

        var exception = Record.Exception(() =>
            PaymentRules.ValidateCancellationOrRefund(payment, "Erreur sur le montant saisi"));

        Assert.Null(exception);
    }
}
