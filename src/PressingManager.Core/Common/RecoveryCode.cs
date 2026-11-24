using System.Security.Cryptography;

namespace PressingManager.Core.Common;

/// <summary>
/// Code de récupération pour « mot de passe oublié » : 12 caractères présentés par groupes de 4
/// (ex. K7QM-2XWD-9PRT). L'alphabet évite les caractères qui se confondent (0/O, 1/I/L).
/// </summary>
public static class RecoveryCode
{
    public const int Length = 12;
    public const int GroupSize = 4;

    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        var groups = Enumerable.Range(0, Length / GroupSize)
            .Select(g => new string(chars, g * GroupSize, GroupSize));

        return string.Join('-', groups);
    }

    /// <summary>Ignore la casse, les espaces et les tirets pour faciliter la saisie. C'est la forme hachée et comparée.</summary>
    public static string Normalize(string? input) =>
        new((input ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
