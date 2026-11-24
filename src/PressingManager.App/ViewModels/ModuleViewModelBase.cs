using CommunityToolkit.Mvvm.ComponentModel;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

/// <summary>
/// Socle commun des ecrans du module traitement / services / stocks (Personne 3) :
/// message de succes ou d'erreur, utilisateur connecte, execution protegee des commandes.
/// Les erreurs metier et les refus de permission venant des services sont affiches, pas propages.
/// </summary>
public abstract partial class ModuleViewModelBase : ObservableObject
{
    protected ModuleViewModelBase(ICurrentUserContext session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));

        // La fenetre est construite avant la connexion : les droits sont relus a chaque changement de session.
        Session.SessionChanged += (_, _) => OnSessionChanged();
    }

    protected ICurrentUserContext Session { get; }

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isError;

    /// <summary>Identifiant de l'utilisateur connecte (exige par les services pour tracer les operations).</summary>
    protected int CurrentUserId =>
        Session.CurrentUser?.Id ?? throw new BusinessRuleException(Loc.Instance["Common_NotSignedIn"]);

    /// <summary>Appele apres connexion ou deconnexion : les ecrans notifient leurs droits et vident leurs listes.</summary>
    protected virtual void OnSessionChanged()
    {
    }

    protected Task RunAsync(Func<Task> action, string? successKey) =>
        RunConfirmedAsync(async () =>
        {
            await action();
            return true;
        }, successKey);

    /// <summary>Comme RunAsync ; l'action retourne false pour signaler qu'elle n'a rien fait (ex. confirmation refusee).</summary>
    protected async Task RunConfirmedAsync(Func<Task<bool>> action, string? successKey)
    {
        Message = null;

        try
        {
            var done = await action();

            if (done && successKey is not null)
            {
                IsError = false;
                Message = Loc.Instance[successKey];
            }
        }
        catch (Exception ex) when (ex is BusinessRuleException or PermissionDeniedException)
        {
            IsError = true;
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            IsError = true;
            Message = Loc.Instance.Format("Common_Unexpected", ex.Message);
        }
    }

    protected static string? NullIfEmpty(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
