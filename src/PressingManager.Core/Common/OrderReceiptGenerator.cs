using System.Globalization;
using System.Text;
using PressingManager.Core.DTOs;

namespace PressingManager.Core.Common;

/// <summary>
/// Reçu de dépôt d'une commande (ticket texte de 40 colonnes).
/// Classe pure : sans interface ni base de données, donc facile à tester.
/// </summary>
public static class OrderReceiptGenerator
{
    private const int Width = 40;
    private const int AmountWidth = 14;

    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public static string FormatTextReceipt(OrderDetailsDto order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var separator = new string('=', Width);
        var thinSeparator = new string('-', Width);

        var sb = new StringBuilder();
        sb.AppendLine(separator);
        sb.AppendLine(Center("PRESSING MANAGER"));
        sb.AppendLine(Center("RECU DE DEPOT DE COMMANDE"));
        sb.AppendLine(separator);
        sb.AppendLine($"Commande      : {order.OrderNumber}");
        sb.AppendLine($"Date          : {order.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", FrenchCulture)}");
        sb.AppendLine($"Client        : {order.CustomerName}");

        if (order.ExpectedDate is { } expected)
        {
            sb.AppendLine($"Retrait prevu : {expected.ToString("dd/MM/yyyy", FrenchCulture)}");
        }

        sb.AppendLine(thinSeparator);

        foreach (var item in order.Items)
        {
            sb.AppendLine(Row($"{item.Quantity} x {item.ItemType}", item.LineTotal));

            if (!string.IsNullOrWhiteSpace(item.Observations))
            {
                sb.AppendLine("   " + Truncate(item.Observations.Trim(), Width - 3));
            }
        }

        sb.AppendLine(thinSeparator);
        sb.AppendLine(Row("Sous-total", order.Subtotal));

        if (order.Discount > 0)
        {
            sb.AppendLine(Row("Remise", -order.Discount));
        }

        if (order.Fees > 0)
        {
            sb.AppendLine(Row("Frais", order.Fees));
        }

        sb.AppendLine(Row("TOTAL", order.Total));
        sb.AppendLine(separator);
        sb.AppendLine(Center("Conservez ce recu pour le retrait."));
        sb.AppendLine(Center("Merci pour votre confiance !"));

        return sb.ToString();
    }

    // Une ligne : libellé à gauche, montant aligné à droite.
    private static string Row(string label, decimal amount)
    {
        var labelWidth = Width - AmountWidth;
        var amountText = amount.ToString("N2", FrenchCulture);
        return Truncate(label, labelWidth - 1).PadRight(labelWidth) + amountText.PadLeft(AmountWidth);
    }

    private static string Center(string text)
    {
        var trimmed = Truncate(text, Width);
        var left = (Width - trimmed.Length) / 2;
        return (new string(' ', left) + trimmed).PadRight(Width);
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text.Substring(0, maxLength);
}