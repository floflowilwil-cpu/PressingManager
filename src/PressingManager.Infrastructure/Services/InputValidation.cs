namespace PressingManager.Infrastructure.Services;

/// <summary>Validations de saisie communes aux services du module traitement / stocks.</summary>
internal static class InputValidation
{
    /// <summary>Texte obligatoire : rogne les espaces, refuse le vide et les textes trop longs.</summary>
    public static string RequireText(string? value, string fieldLabel, int maxLength)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException($"{fieldLabel} est obligatoire.", nameof(value));
        }

        if (text.Length > maxLength)
        {
            throw new ArgumentException($"{fieldLabel} ne doit pas dépasser {maxLength} caractères.", nameof(value));
        }

        return text;
    }

    /// <summary>Texte facultatif : null si vide, sinon rogne et limite en longueur.</summary>
    public static string? OptionalText(string? value, string fieldLabel, int maxLength)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length > maxLength)
        {
            throw new ArgumentException($"{fieldLabel} ne doit pas dépasser {maxLength} caractères.", nameof(value));
        }

        return text;
    }

    /// <summary>Convertit une date en UTC (les dates sont toujours stockees en UTC).</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => value
    };
}
