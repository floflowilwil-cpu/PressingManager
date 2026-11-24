using System.Globalization;

namespace PressingManager.Core.Common;

public static class MoneyFormatter
{
    /// <summary>Montant avec séparateur de milliers (espace normal) et virgule décimale : « 12 500,00 ».</summary>
    public static string Format(decimal amount) =>
        amount.ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(',', ' ').Replace('.', ',');
}
