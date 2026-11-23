using System.Globalization;
using System.Text;
using PressingManager.Core.DTOs;

namespace PressingManager.Core.Common;

/// <summary>
/// Export CSV compatible avec Excel en francais : separateur ';', montants avec virgule.
/// Pour ecrire le fichier : <c>File.WriteAllText(path, csv, new UTF8Encoding(true))</c> (BOM UTF-8,
/// sinon Excel affiche mal les accents).
/// </summary>
public static class CsvExporter
{
    public const char Separator = ';';

    public static string Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        AppendLine(builder, headers);

        foreach (var row in rows)
        {
            AppendLine(builder, row);
        }

        return builder.ToString();
    }

    public static string OrdersToCsv(IEnumerable<OrderReportRow> rows)
    {
        var headers = new[] { "Numéro", "Date", "Client", "Statut", "Total", "Payé", "Solde" };

        var lines = rows.Select(r => (IReadOnlyList<string>)new[]
        {
            Sanitize(r.OrderNumber),
            r.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            Sanitize(r.CustomerName),
            OrderStatusLabels.ToFrench(r.Status),
            FormatMoney(r.Total),
            FormatMoney(r.AmountPaid),
            FormatMoney(r.Balance)
        });

        return Build(headers, lines);
    }

    /// <summary>Montant a 2 decimales avec virgule, independant de la culture de la machine.</summary>
    public static string FormatMoney(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>
    /// Neutralise l'injection de formules Excel : un texte commencant par = + - @ est prefixe d'une apostrophe.
    /// A appliquer aux textes saisis par des utilisateurs, pas aux montants.
    /// </summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return "=+-@\t\r".Contains(value[0]) ? "'" + value : value;
    }

    private static void AppendLine(StringBuilder builder, IEnumerable<string> cells) =>
        builder.Append(string.Join(Separator, cells.Select(Escape))).Append("\r\n");

    private static string Escape(string value)
    {
        if (value.IndexOfAny(new[] { Separator, '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
