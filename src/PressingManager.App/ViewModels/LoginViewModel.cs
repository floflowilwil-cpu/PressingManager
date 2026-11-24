using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.Core.Common;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

/// <summary>
/// Ecran de connexion. Au tout premier lancement (aucun compte), il bascule en mode
/// "creation du compte administrateur" : aucun mot de passe par defaut n'est stocke dans le code.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public LoginViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle), nameof(SubmitLabel))]
    private bool _isSetupMode;

    public string Subtitle => IsSetupMode
        ? "Première utilisation : créez le compte administrateur"
        : "Connexion";

    public string SubmitLabel => IsSetupMode ? "Créer le compte et se connecter" : "Se connecter";

    /// <summary>Levé quand l'utilisateur est connecté ; la fenêtre se ferme alors avec DialogResult = true.</summary>
    public event EventHandler? LoginSucceeded;

    public async Task InitializeAsync()
    {
        try
        {
            IsSetupMode = await _auth.IsSetupRequiredAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Impossible d'accéder à la base de données : {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = true;

        try
        {
            if (IsSetupMode)
            {
                if (Password != ConfirmPassword)
                {
                    ErrorMessage = "Les mots de passe ne correspondent pas.";
                    return;
                }

                await _auth.CreateInitialAdministratorAsync(FullName, Username, Password);
            }

            var result = await _auth.LoginAsync(Username, Password);

            if (result.IsSuccess)
            {
                Password = string.Empty;
                ConfirmPassword = string.Empty;
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (BusinessRuleException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur inattendue : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
