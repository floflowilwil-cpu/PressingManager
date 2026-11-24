using System.Globalization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.Tests.Unit;

public class OrderReceiptGeneratorTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static OrderDetailsDto BuildOrder(
        decimal discount = 0m, decimal fees = 0m, string itemType = "Chemise", string? observations = null)
    {
        var items = new List<OrderItemDto>
        {
            new(1, 1, itemType, 2, 1500m, 3000m, observations),
            new(2, 2, "Costume", 1, 2500m, 2500m, null)
        };

        return new OrderDetailsDto(
            1, "CMD-20261123-0001", 1, "Awa Diallo",
            new DateTime(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 26),
            OrderStatus.Received,
            5500m, discount, fees, 5500m - discount + fees,
            null, items);
    }

    [Fact]
    public void Receipt_ContainsOrderCustomerItemsAndTotal()
    {
        var text = OrderReceiptGenerator.FormatTextReceipt(BuildOrder(discount: 500m, fees: 200m));

        Assert.Contains("CMD-20261123-0001", text);
        Assert.Contains("Awa Diallo", text);
        Assert.Contains("2 x Chemise", text);
        Assert.Contains("1 x Costume", text);
        Assert.Contains("26/11/2026", text);
        Assert.Contains(5200m.ToString("N2", French), text);
    }

    [Fact]
    public void Receipt_ShowsDiscountAndFees_OnlyWhenPresent()
    {
        var without = OrderReceiptGenerator.FormatTextReceipt(BuildOrder());
        var with = OrderReceiptGenerator.FormatTextReceipt(BuildOrder(discount: 500m, fees: 200m));

        Assert.DoesNotContain("Remise", without);
        Assert.DoesNotContain("Frais", without);
        Assert.Contains("Remise", with);
        Assert.Contains("Frais", with);
    }

    [Fact]
    public void Receipt_NeverExceeds40Columns()
    {
        var longName = new string('X', 80);
        var text = OrderReceiptGenerator.FormatTextReceipt(
            BuildOrder(itemType: longName, observations: new string('o', 120)));

        foreach (var line in text.Split('\n'))
        {
            Assert.True(line.TrimEnd('\r').Length <= 40, $"Ligne trop longue : {line}");
        }
    }

    [Fact]
    public void Receipt_NullOrder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => OrderReceiptGenerator.FormatTextReceipt(null!));
    }
}