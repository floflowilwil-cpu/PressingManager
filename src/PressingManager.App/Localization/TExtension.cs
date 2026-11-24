using System.Windows.Data;
using System.Windows.Markup;

namespace PressingManager.App.Localization;

/// <summary>
/// Extension XAML : <c>Text="{loc:T Login_Welcome}"</c>.
/// Cree une liaison sur <see cref="Loc"/> : le texte change tout seul quand l'utilisateur change de langue.
/// </summary>
public sealed class TExtension : MarkupExtension
{
    public TExtension()
    {
    }

    public TExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
