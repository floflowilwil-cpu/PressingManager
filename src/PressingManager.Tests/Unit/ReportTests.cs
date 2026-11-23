using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.Tests.Unit;

public class ReportCalculatorTests
{
    private static OrderReportRow Row(string number, OrderStatus status, decimal total, decimal paid, int day = 1) =>
        new(number, new DateTime(2026, 11, day, 9, 0, 0, DateTimeKind.Utc), "Client " + number, status, total, paid);

    [Fact]
    public void Balance_IsTotalMinusAmountPaid()
    {
        Assert.Equal(1500m, Row("A", OrderStatus.Received, 5000m, 3500m).Balance);
    }

    [Fact]
    public void CountByStatus_ContainsEveryStatus()
    {
        var counts = ReportCalculator.CountByStatus(new[]
        {
            Row("A", OrderStatus.Received, 100m, 0m),
            Row("B", OrderStatus.Received, 100m, 0m),
            Row("C", OrderStatus.Delivered, 100m, 100m)
        });

        Assert.Equal(Enum.GetValues<OrderStatus>().Length, counts.Count);
        Assert.Equal(2, counts[OrderStatus.Received]);
        Assert.Equal(1, counts[OrderStatus.Delivered]);
        Assert.Equal(0, counts[OrderStatus.Cleaning]);
    }

    [Fact]
    public void GetOutstanding_ExcludesPaidAndCancelled_AndSortsByBalanceDescending()
    {
        var result = ReportCalculator.GetOutstanding(new[]
        {
            Row("A", OrderStatus.Received, 1000m, 400m),
            Row("B", OrderStatus.Delivered, 1000m, 1000m),
            Row("C", OrderStatus.Cancelled, 9000m, 0m),
            Row("D", OrderStatus.Ironing, 3000m, 0m)
        });

        Assert.Equal(new[] { "D", "A" }, result.Select(r => r.OrderNumber));
    }

    [Fact]
    public void GetAwaitingPickup_ReturnsReadyOrdersOldestFirst()
    {
        var result = ReportCalculator.GetAwaitingPickup(new[]
        {
            Row("A", OrderStatus.ReadyForPickup, 100m, 0m, day: 10),
            Row("B", OrderStatus.Cleaning, 100m, 0m, day: 2),
            Row("C", OrderStatus.ReadyForPickup, 100m, 0m, day: 3)
        });

        Assert.Equal(new[] { "C", "A" }, result.Select(r => r.OrderNumber));
    }

    [Fact]
    public void Summarize_IgnoresCancelledOrders_AndComputesNetResult()
    {
        var summary = ReportCalculator.Summarize(
            new[]
            {
                Row("A", OrderStatus.Delivered, 5000m, 5000m),
                Row("B", OrderStatus.Received, 2000m, 500m),
                Row("C", OrderStatus.Cancelled, 8000m, 0m)
            },
            totalCollected: 5500m,
            totalExpenses: 1200m);

        Assert.Equal(2, summary.OrderCount);
        Assert.Equal(7000m, summary.TotalBilled);
        Assert.Equal(1500m, summary.OutstandingBalance);
        Assert.Equal(4300m, summary.NetResult);
    }
}

public class CsvExporterTests
{
    [Theory]
    [InlineData(12.5, "12,50")]
    [InlineData(0, "0,00")]
    [InlineData(1000, "1000,00")]
    [InlineData(-3.456, "-3,46")]
    public void FormatMoney_UsesTwoDecimalsAndACommaWhateverTheCulture(double value, string expected)
    {
        Assert.Equal(expected, CsvExporter.FormatMoney((decimal)value));
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+33", "'+33")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("Jean", "Jean")]
    [InlineData("", "")]
    public void Sanitize_NeutralisesSpreadsheetFormulas(string input, string expected)
    {
        Assert.Equal(expected, CsvExporter.Sanitize(input));
    }

    [Fact]
    public void Build_QuotesCellsContainingSeparatorsQuotesOrNewlines()
    {
        var csv = CsvExporter.Build(
            new[] { "A", "B" },
            new[] { (IReadOnlyList<string>)new[] { "x;y", "dit \"oui\"" } });

        Assert.Equal("A;B\r\n\"x;y\";\"dit \"\"oui\"\"\"\r\n", csv);
    }

    [Fact]
    public void OrdersToCsv_WritesHeaderAndFrenchStatusAndBalance()
    {
        var csv = CsvExporter.OrdersToCsv(new[]
        {
            new OrderReportRow("CMD-001", new DateTime(2026, 11, 23, 10, 30, 0), "=Marie", OrderStatus.ReadyForPickup, 5000m, 2000m)
        });

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Numéro;Date;Client;Statut;Total;Payé;Solde", lines[0]);
        Assert.Equal("CMD-001;2026-11-23 10:30;'=Marie;Prêt à récupérer;5000,00;2000,00;3000,00", lines[1]);
    }
}
