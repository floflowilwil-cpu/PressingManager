using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.Views;

/// <summary>
/// Onglet Paramètres : compte de l'utilisateur connecté, langue de l'interface,
/// changement de mot de passe et déconnexion.
/// </summary>
public partial class SettingsView : UserControl
{
    private readonly ICurrentUserContext _session;
    private readonly IUserService _users;
    private readonly IAuthService _auth;

    public SettingsView(ICurrentUserContext session, IUserService users, IAuthService auth)
    {
        InitializeComponent();

        _session = session;
        _users = users;
        _auth = auth;

        Loc.Instance.LanguageChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>À appeler à chaque ouverture de l'onglet.</summary>
    public Task RefreshAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        var user = _session.CurrentUser;
        TxtFullName.Text = user?.FullName ?? "—";
        TxtUsername.Text = user?.Username ?? "—";
        TxtRole.Text = user is null ? "—" : Loc.Instance.Role(user.Role);

        ChipFrench.IsChecked = !Loc.Instance.IsEnglish;
        ChipEnglish.IsChecked = Loc.Instance.IsEnglish;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        TxtVersion.Text = Loc.Instance.Format("Settings_Version", version is null ? "1.0" : $"{version.Major}.{version.Minor}");
    }

    private void ChipFrench_Click(object sender, RoutedEventArgs e) => Loc.Instance.SetLanguage(AppLanguage.Fr);

    private void ChipEnglish_Click(object sender, RoutedEventArgs e) => Loc.Instance.SetLanguage(AppLanguage.En);

    private async void BtnChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var current = CurrentPasswordBox.Password;
        var next = NewPasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;

        if (current.Length == 0 || next.Length == 0 || confirm.Length == 0)
        {
            ShowPasswordMessage(Loc.Instance["Settings_PasswordFill"], true);
            return;
        }

        if (next != confirm)
        {
            ShowPasswordMessage(Loc.Instance["Settings_PasswordMismatch"], true);
            return;
        }

        try
        {
            await _users.ChangeOwnPasswordAsync(current, next);

            CurrentPasswordBox.Clear();
            NewPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            ShowPasswordMessage(Loc.Instance["Settings_PasswordChanged"], false);
        }
        catch (Exception ex) when (ex is BusinessRuleException or PermissionDeniedException)
        {
            ShowPasswordMessage(ex.Message, true);
        }
        catch (Exception ex)
        {
            ShowPasswordMessage(Loc.Instance.Format("Common_Unexpected", ex.Message), true);
        }
    }

    private void ShowPasswordMessage(string text, bool isError)
    {
        TxtPasswordMessage.Text = text;
        TxtPasswordMessage.Foreground = new SolidColorBrush(isError ? Color.FromRgb(192, 57, 43) : Color.FromRgb(30, 132, 73));
    }

    /// <summary>Se déconnecte puis relance l'application sur l'écran de connexion.</summary>
    private async void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            Loc.Instance["Settings_LogoutConfirm"],
            "Pressing Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _auth.LogoutAsync();
        }
        catch (Exception)
        {
            // La déconnexion ne doit jamais bloquer : on relance quand même.
        }

        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            var info = new ProcessStartInfo(exe) { UseShellExecute = true };

            // Lancé avec « dotnet PressingManager.App.dll » : il faut redonner la DLL à dotnet.
            if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                info.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
            }

            Process.Start(info);
        }

        Application.Current.Shutdown();
    }
}
