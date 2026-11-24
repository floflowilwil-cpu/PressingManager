using System.ComponentModel;
using System.Globalization;
using System.IO;
using PressingManager.Core.Enums;

namespace PressingManager.App.Localization;

public enum AppLanguage
{
    Fr,
    En
}

/// <summary>
/// Langue de l'interface (francais par defaut) et acces aux textes. Un seul exemplaire pour toute l'application.
/// En XAML : <c>Text="{loc:T Stats_Title}"</c> (se met a jour tout seul quand la langue change).
/// En C# : <c>Loc.Instance["Stats_Title"]</c> ou <c>Loc.Instance.Format("Stats_Rows", n)</c>.
/// Le choix est memorise dans %LocalAppData%\PressingManager\language.txt.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private AppLanguage _language = AppLanguage.Fr;

    private Loc()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Levee apres chaque changement de langue (pour recalculer les listes deja construites).</summary>
    public event EventHandler? LanguageChanged;

    public AppLanguage Language => _language;

    public bool IsEnglish => _language == AppLanguage.En;

    public CultureInfo Culture => CultureInfo.GetCultureInfo(IsEnglish ? "en-US" : "fr-FR");

    /// <summary>Texte dans la langue courante (francais si la cle manque en anglais, la cle elle-meme sinon).</summary>
    public string this[string key] => Strings.Get(_language, key);

    public string Format(string key, params object?[] args) => string.Format(Culture, this[key], args);

    public string Status(OrderStatus status) => this[$"Status_{status}"];

    public string Role(UserRole role) => this[$"Role_{role}"];

    public void SetLanguage(AppLanguage language)
    {
        if (language == _language)
        {
            return;
        }

        _language = language;
        Save();

        // "Item[]" invalide toutes les liaisons {loc:T ...} ; les autres noms servent aux liaisons directes.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnglish)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A appeler une fois au demarrage, avant d'afficher la premiere fenetre.</summary>
    public void LoadSaved()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                _language = File.ReadAllText(SettingsPath).Trim().Equals("en", StringComparison.OrdinalIgnoreCase)
                    ? AppLanguage.En
                    : AppLanguage.Fr;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preference illisible : on reste en francais.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, _language == AppLanguage.En ? "en" : "fr");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ce n'est qu'une preference : on ne bloque pas l'application.
        }
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PressingManager",
        "language.txt");
}
