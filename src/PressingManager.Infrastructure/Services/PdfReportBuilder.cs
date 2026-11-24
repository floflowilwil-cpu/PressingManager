using System.Globalization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Construit le PDF du rapport des commandes avec QuestPDF (licence Community : gratuite pour un projet
/// scolaire ou une petite entreprise ; à revoir si le logiciel devient commercial à grande échelle).
/// Classe sans accès base de données : elle ne fait que mettre en page les lignes reçues.
/// </summary>
internal static class PdfReportBuilder
{
    private const string Navy = "#0F2A4A";
    private const string Turquoise = "#14B8A6";

    static PdfReportBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void BuildOrdersReport(
        IReadOnlyList<OrderReportRow> rows,
        string description,
        DateTime generatedAtLocal,
        string filePath)
    {
        // Les commandes annulées restent listées mais ne comptent pas dans les totaux (comme ReportCalculator).
        var counted = rows.Where(r => r.Status != OrderStatus.Cancelled).ToList();

        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("Pressing Manager — Rapport des commandes")
                        .FontSize(18).Bold().FontColor(Navy);
                    column.Item().Text(description).FontColor(Turquoise).SemiBold();
                    column.Item().Text($"Généré le {generatedAtLocal:dd/MM/yyyy HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(95);   // numéro
                        columns.ConstantColumn(95);   // date
                        columns.RelativeColumn();     // client
                        columns.ConstantColumn(125);  // statut
                        columns.ConstantColumn(80);   // total
                        columns.ConstantColumn(80);   // payé
                        columns.ConstantColumn(80);   // solde
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Numéro", alignRight: false);
                        HeaderCell(header.Cell(), "Date", alignRight: false);
                        HeaderCell(header.Cell(), "Client", alignRight: false);
                        HeaderCell(header.Cell(), "Statut", alignRight: false);
                        HeaderCell(header.Cell(), "Total", alignRight: true);
                        HeaderCell(header.Cell(), "Payé", alignRight: true);
                        HeaderCell(header.Cell(), "Solde", alignRight: true);
                    });

                    foreach (var row in rows)
                    {
                        BodyCell(table.Cell(), row.OrderNumber, alignRight: false);
                        BodyCell(table.Cell(), row.CreatedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), alignRight: false);
                        BodyCell(table.Cell(), row.CustomerName, alignRight: false);
                        BodyCell(table.Cell(), OrderStatusLabels.ToFrench(row.Status), alignRight: false);
                        BodyCell(table.Cell(), MoneyFormatter.Format(row.Total), alignRight: true);
                        BodyCell(table.Cell(), MoneyFormatter.Format(row.AmountPaid), alignRight: true);
                        BodyCell(table.Cell(), MoneyFormatter.Format(row.Balance), alignRight: true);
                    }

                    TotalCell(table.Cell().ColumnSpan(4), $"Total ({counted.Count} commande(s), hors annulées)", alignRight: true);
                    TotalCell(table.Cell(), MoneyFormatter.Format(counted.Sum(r => r.Total)), alignRight: true);
                    TotalCell(table.Cell(), MoneyFormatter.Format(counted.Sum(r => r.AmountPaid)), alignRight: true);
                    TotalCell(table.Cell(), MoneyFormatter.Format(counted.Sum(r => r.Balance)), alignRight: true);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken1));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf(filePath);
    }

    private static void HeaderCell(IContainer cell, string label, bool alignRight)
    {
        var aligned = alignRight ? cell.Background(Navy).Padding(5).AlignRight() : cell.Background(Navy).Padding(5);
        aligned.Text(label).FontColor(Colors.White).Bold();
    }

    private static void BodyCell(IContainer cell, string value, bool alignRight)
    {
        var padded = cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5);
        (alignRight ? padded.AlignRight() : padded).Text(value);
    }

    private static void TotalCell(IContainer cell, string value, bool alignRight)
    {
        var padded = cell.Background(Colors.Grey.Lighten3).Padding(5);
        (alignRight ? padded.AlignRight() : padded).Text(value).Bold().FontColor(Navy);
    }
}
