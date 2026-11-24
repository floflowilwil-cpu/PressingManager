using System.Globalization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Construit le recu officiel de paiement au format PDF avec QuestPDF (licence Community).
/// </summary>
public static class PaymentReceiptPdfBuilder
{
    private const string Navy = "#0F2A4A";
    private const string Turquoise = "#14B8A6";
    private const string Emerald = "#10B981";
    private const string EmeraldLight = "#ECFDF5";
    private const string Amber = "#D97706";
    private const string AmberLight = "#FEF3C7";
    private const string SlateLight = "#F8FAFC";
    private const string BorderGrey = "#E2E8F0";

    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    static PaymentReceiptPdfBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void BuildReceiptPdf(PaymentReceiptDto receipt, string filePath)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        CreateDocument(receipt).GeneratePdf(filePath);
    }

    public static byte[] BuildReceiptPdfBytes(PaymentReceiptDto receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return CreateDocument(receipt).GeneratePdf();
    }

    private static IDocument CreateDocument(PaymentReceiptDto receipt)
    {
        var statusText = receipt.IsFullyPaid ? "COMMANDE SOLDEE" : "PAIEMENT PARTIEL";
        var statusBg = receipt.IsFullyPaid ? EmeraldLight : AmberLight;
        var statusColor = receipt.IsFullyPaid ? Emerald : Amber;
        var localDateText = receipt.PaidAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", FrenchCulture);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor(Navy));

                page.Header().Background(Navy).Padding(14).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("PRESSING MANAGER")
                            .FontSize(15).Bold().FontColor(Colors.White);
                        col.Item().Text("RECU OFFICIEL DE PAIEMENT")
                            .FontSize(9).SemiBold().FontColor(Turquoise);
                    });

                    row.AutoItem().AlignMiddle().Background(Turquoise).PaddingHorizontal(10).PaddingVertical(5)
                        .Text(receipt.ReceiptNumber)
                        .FontSize(9).Bold().FontColor(Colors.White);
                });

                page.Content().PaddingVertical(14).Column(column =>
                {
                    column.Spacing(10);

                    // Bandeau de validation visuelle
                    column.Item().Background(EmeraldLight).Border(1).BorderColor("#A7F3D0").Padding(12).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PAIEMENT VALIDE AVEC SUCCES")
                                .FontSize(12).Bold().FontColor("#065F46");
                            col.Item().Text($"Enregistre le {localDateText}")
                                .FontSize(9).FontColor("#047857");
                        });

                        row.AutoItem().AlignMiddle().Background(statusBg).Border(1).BorderColor(statusColor)
                            .PaddingHorizontal(8).PaddingVertical(4)
                            .Text(statusText).FontSize(8).Bold().FontColor(statusColor);
                    });

                    // Informations client / commande / paiement
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });

                        InfoCell(table.Cell(), "CLIENT", receipt.CustomerName);
                        InfoCell(table.Cell(), "N° COMMANDE", receipt.OrderNumber);
                        InfoCell(table.Cell(), "MODE DE PAIEMENT", receipt.MethodLabel);
                        InfoCell(table.Cell(), "CAISSIER", receipt.CashierName);

                        if (!string.IsNullOrWhiteSpace(receipt.Reference))
                        {
                            InfoCell(table.Cell().ColumnSpan(2), "REFERENCE TRANSACTION", receipt.Reference);
                        }
                    });

                    // Tableau financier
                    column.Item().Background(SlateLight).Border(1).BorderColor(BorderGrey).Padding(12).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.ConstantColumn(115);
                        });

                        AmountRow(table, "Total de la commande", MoneyFormatter.Format(receipt.OrderTotal), false, Navy);
                        AmountRow(table, "Montant verse (ce recu)", MoneyFormatter.Format(receipt.AmountPaid), true, Emerald);
                        AmountRow(table, "Total deja paye", MoneyFormatter.Format(receipt.TotalPaidOnOrder), false, Navy);
                        AmountRow(table, "Solde restant a payer", MoneyFormatter.Format(receipt.RemainingBalance), true, statusColor);
                    });

                    column.Item().PaddingTop(8).AlignCenter().Text($"Justificatif N° {receipt.ReceiptNumber}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignCenter().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(BorderGrey);
                    col.Item().PaddingTop(6).AlignCenter()
                        .Text("Merci pour votre confiance ! Conservez ce justificatif.")
                        .FontSize(9).SemiBold().FontColor(Turquoise);
                });
            });
        });
    }

    private static void InfoCell(IContainer cell, string label, string value)
    {
        cell.PaddingVertical(4).PaddingRight(8).Column(col =>
        {
            col.Item().Text(label).FontSize(8).Bold().FontColor("#64748B");
            col.Item().Text(value).FontSize(10).SemiBold().FontColor(Navy);
        });
    }

    private static void AmountRow(TableDescriptor table, string label, string formattedAmount, bool highlight, string amountColor)
    {
        var labelText = table.Cell().PaddingVertical(3).Text(label).FontSize(highlight ? 11 : 9.5f);
        if (highlight)
        {
            labelText.Bold();
        }

        var valueText = table.Cell().PaddingVertical(3).AlignRight().Text(formattedAmount)
            .FontSize(highlight ? 12 : 9.5f).FontColor(amountColor);
        if (highlight)
        {
            valueText.Bold();
        }
    }
}
