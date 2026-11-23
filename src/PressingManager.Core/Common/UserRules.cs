namespace PressingManager.Core.Common;

/// <summary>Regles de validation des mots de passe.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>Retourne un message d'erreur, ou null si le mot de passe est acceptable.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return $"Le mot de passe doit contenir au moins {MinLength} caractères.";
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return "Le mot de passe doit contenir au moins une lettre et un chiffre.";
        }

        return null;
    }
}

/// <summary>Regles de validation d'un compte utilisateur (fonctions pures).</summary>
public static class UserRules
{
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 50;
    public const int MaxFullNameLength = 100;

    public static string NormalizeUsername(string? username) =>
        (username ?? string.Empty).Trim().ToLowerInvariant();

    public static string? ValidateUsername(string? username)
    {
        var normalized = NormalizeUsername(username);

        if (normalized.Length is < MinUsernameLength or > MaxUsernameLength)
        {
            return $"Le nom d'utilisateur doit contenir entre {MinUsernameLength} et {MaxUsernameLength} caractères.";
        }

        if (!normalized.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
        {
            return "Le nom d'utilisateur ne peut contenir que des lettres, chiffres, '.', '_' et '-'.";
        }

        return null;
    }

    public static string? ValidateFullName(string? fullName)
    {
        var name = fullName?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return "Le nom complet est obligatoire.";
        }

        return name.Length > MaxFullNameLength
            ? $"Le nom complet ne doit pas dépasser {MaxFullNameLength} caractères."
            : null;
    }

    /// <summary>Valide les trois champs d'un nouveau compte ; retourne la premiere erreur ou null.</summary>
    public static string? ValidateNewUser(string? fullName, string? username, string? password) =>
        ValidateFullName(fullName) ?? ValidateUsername(username) ?? PasswordPolicy.Validate(password);
}
