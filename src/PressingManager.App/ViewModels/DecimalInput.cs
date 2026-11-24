using System.Globalization;
using PressingManager.App.Localization;

namespace PressingManager.App.ViewModels;

/// <summary>Saisie et affichage des nombres decimaux (virgule ou point acceptes, espaces ignores).</summary>
internal static class DecimalInput
{
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text
            .Trim()
            .Replace("\u00A0", string.Empty)
            .Replace("\u202F", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(',', '.');

        return decimal.TryParse(
            cleaned,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    /// <summary>Quantite sans zeros inutiles : 12 ; 12,5 ; 0,25.</summary>
    public static string Format(decimal value) => value.ToString("0.##", Loc.Instance.Culture);
}
