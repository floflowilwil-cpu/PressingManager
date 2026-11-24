using System.Text;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;

namespace PressingManager.Tests.Unit;

public class PaymentReceiptGeneratorTests
{
    [Fact]
    public void FormatReceiptNumber_FormatsDateAndPaddedId()
    {
        var date = new DateTime(2026, 11, 23, 14, 30, 0, DateTimeKind.Utc);

        Assert.Equal("REC-20261123-0007", PaymentReceiptGenerator.FormatReceiptNumber(date, 7));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void FormatReceiptNumber_RefusesNonPositivePaymentId(int invalidId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaymentReceiptGenerator.FormatReceiptNumber(DateTime.UtcNow, invalidId));
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, "Espèces")]
    [InlineData(PaymentMethod.MtnMobileMoney, "MTN Mobile Money")]
    [InlineData(PaymentMethod.OrangeMoney, "Orange Money")]
    [InlineData(PaymentMethod.Other, "Autre")]
    public void GetMethodLabel_ReturnsFrenchLabel(PaymentMethod method, string expected)
    {
        Assert.Equal(expected, PaymentReceiptGenerator.GetMethodLabel(method));
    }

    [Theory]
    [InlineData(PaymentStatus.Valid, "Validé")]
    [InlineData(PaymentStatus.Cancelled, "Annulé")]
    [InlineData(PaymentStatus.Refunded, "Remboursé")]
    public void GetStatusLabel_ReturnsFrenchLabel(PaymentStatus status, string expected)
    {
        Assert.Equal(expected, PaymentReceiptGenerator.GetStatusLabel(status));
    }

    [Fact]
    public void BuildReceipt_And_FormatTextReceipt_HandlePartialAndFullPayments()
    {
        var paidAt = new DateTime(2026, 11, 23, 16, 45, 0, DateTimeKind.Utc);
        var payment = new PaymentDto(
            Id: 12,
            OrderId: 5,
            Amount: 3500m,
            PaidAt: paidAt,
            Method: PaymentMethod.MtnMobileMoney,
            Reference: "MTN-8899",
            Status: PaymentStatus.Valid,
            ReceivedByUserId: 2,
            CashSessionId: null,
            CancelReason: null,
            CancelledByUserId: null,
            CancelledAt: null);

        var partial = PaymentReceiptGenerator.BuildReceipt(
            payment, "CMD-20261123-0005", "Marie Claire", 10000m, 3500m, "Paul Caissier");

        Assert.Equal("REC-20261123-0012", partial.ReceiptNumber);
        Assert.Equal(6500m, partial.RemainingBalance);
        Assert.False(partial.IsFullyPaid);

        var partialText = PaymentReceiptGenerator.FormatTextReceipt(partial);
        Assert.Contains("REC-20261123-0012", partialText);
        Assert.Contains("MTN-8899", partialText);
        Assert.Contains("PAIEMENT PARTIEL", partialText);

        var full = PaymentReceiptGenerator.BuildReceipt(
            payment, "CMD-20261123-0005", "Marie Claire", 10000m, 10000m, "Paul Caissier");
        Assert.True(full.IsFullyPaid);
        Assert.Contains("SOLDE", PaymentReceiptGenerator.FormatTextReceipt(full));
    }

    [Fact]
    public void BuildReceiptPdf_WritesValidPdfFileWithQuestPdf()
    {
        var payment = new PaymentDto(
            Id: 21,
            OrderId: 9,
            Amount: 5000m,
            PaidAt: new DateTime(2026, 11, 24, 9, 0, 0, DateTimeKind.Utc),
            Method: PaymentMethod.OrangeMoney,
            Reference: "OM-7788",
            Status: PaymentStatus.Valid,
            ReceivedByUserId: 1,
            CashSessionId: null,
            CancelReason: null,
            CancelledByUserId: null,
            CancelledAt: null);

        var receipt = PaymentReceiptGenerator.BuildReceipt(
            payment, "CMD-20261124-0009", "Awa Nkodo", 5000m, 5000m, "Paul Caissier");

        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"pressing-receipt-test-{Guid.NewGuid():N}.pdf");
        try
        {
            PaymentReceiptPdfBuilder.BuildReceiptPdf(receipt, tempPdfPath);

            Assert.True(File.Exists(tempPdfPath));
            var bytes = File.ReadAllBytes(tempPdfPath);
            Assert.True(bytes.Length > 500);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            if (File.Exists(tempPdfPath))
            {
                File.Delete(tempPdfPath);
            }
        }
    }
}
