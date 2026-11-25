
using System;
using PressingManager.App.Localization;

namespace PressingManager.App.Services;

public sealed class LanguageService
{
    public bool IsEnglish => Loc.Instance.IsEnglish;

    public event EventHandler? LanguageChanged;

    public LanguageService()
    {
        Loc.Instance.LanguageChanged += (_, _) =>
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public void SetLanguage(bool isEnglish)
    {
        Loc.Instance.SetLanguage(
            isEnglish ? AppLanguage.En : AppLanguage.Fr);
    }

    public string Translate(string french, string english)
    {
        return IsEnglish ? english : french;
    }
}
