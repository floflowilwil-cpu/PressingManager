
using System;

namespace PressingManager.App.Services;

public sealed class LanguageService
{
    public bool IsEnglish { get; private set; }

    public event EventHandler? LanguageChanged;

    public void SetLanguage(bool isEnglish)
    {
        if (IsEnglish == isEnglish)
            return;

        IsEnglish = isEnglish;

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Translate(string french, string english)
    {
        return IsEnglish ? english : french;
    }
}
