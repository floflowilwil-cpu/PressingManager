using System.Globalization;

namespace PressingManager.Core.Common;

public static class MoneyFormatter
{
    /// <summary>Montant avec séparateur de milliers (espace normal) et virgule décimale : « 12 500,00 ».</summary>
    public static string Format(decimal amount) =>
        amount.ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(',', ' ').Replace('.', ',');

    /// <summary>
    /// Montant en francs CFA : le FCFA n'a pas de centimes, donc arrondi à l'unité (« 12 500 FCFA »).
    /// Avec <paramref name="withCurrency"/> = false, seulement le nombre (pour les colonnes dont l'en-tête indique FCFA).
    /// </summary>
    public static string FormatFcfa(decimal amount, bool withCurrency = true)
    {
        var rounded = Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        var text = rounded.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', ' ');
        return withCurrency ? text + " FCFA" : text;
    }
}
