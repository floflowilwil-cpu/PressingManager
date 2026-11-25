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
    private readonly IIntegrationSettingsStore _settingsStore;
    private readonly IEmailNotificationService _emailService;

    public SettingsView(
        ICurrentUserContext session,
        IUserService users,
        IAuthService auth,
        IIntegrationSettingsStore settingsStore,
        IEmailNotificationService emailService)
    {
        InitializeComponent();

        _session = session;
        _users = users;
        _auth = auth;
        _settingsStore = settingsStore;
        _emailService = emailService;

        Loc.Instance.LanguageChanged += (_, _) => Refresh();
        LoadIntegrationSettings();
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

    private void LoadIntegrationSettings()
    {
        var settings = _settingsStore.Load();

        ChkSmtpEnabled.IsChecked = settings.Email.Enabled;
        TxtSmtpHost.Text = settings.Email.SmtpHost;
        TxtSmtpPort.Text = settings.Email.SmtpPort.ToString();
        ChkSmtpSsl.IsChecked = settings.Email.UseSsl;
        TxtSmtpUsername.Text = settings.Email.Username;
        TxtSmtpPassword.Password = settings.Email.Password;
        TxtSmtpFromAddress.Text = settings.Email.FromAddress;
        TxtSmtpFromName.Text = settings.Email.FromName;

        var isCampay = string.Equals(settings.MobileMoney.Provider, "Campay", StringComparison.OrdinalIgnoreCase);
        ChipMomoCampay.IsChecked = isCampay;
        ChipMomoSimulation.IsChecked = !isCampay;
        TxtMomoBaseUrl.Text = settings.MobileMoney.BaseUrl;
        TxtMomoApiToken.Text = settings.MobileMoney.ApiToken;
        TxtMomoUsername.Text = settings.MobileMoney.Username;
        TxtMomoPassword.Password = settings.MobileMoney.Password;
    }

    private bool TrySaveSmtpSettings()
    {
        if (!int.TryParse(TxtSmtpPort.Text.Trim(), out var port) || port < 1 || port > 65535)
        {
            ShowStatusText(TxtSmtpMessage, Loc.Instance["Settings_Smtp_InvalidPort"], isError: true);
            return false;
        }

        var current = _settingsStore.Load();
        current.Email.Enabled = ChkSmtpEnabled.IsChecked == true;
        current.Email.SmtpHost = TxtSmtpHost.Text.Trim();
        current.Email.SmtpPort = port;
        current.Email.UseSsl = ChkSmtpSsl.IsChecked == true;
        current.Email.Username = TxtSmtpUsername.Text.Trim();
        current.Email.Password = TxtSmtpPassword.Password;

        var from = TxtSmtpFromAddress.Text.Trim();
        if (string.IsNullOrWhiteSpace(from) && current.Email.Username.Contains('@'))
        {
            from = current.Email.Username;
            TxtSmtpFromAddress.Text = from;
        }

        current.Email.FromAddress = from;
        current.Email.FromName = TxtSmtpFromName.Text.Trim();

        _settingsStore.Save(current);
        return true;
    }

    private void BtnOpenOutbox_Click(object sender, RoutedEventArgs e)
    {
        var outbox = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PressingManager",
            "outbox");
        Directory.CreateDirectory(outbox);
        Process.Start(new ProcessStartInfo(outbox) { UseShellExecute = true });
    }

    private void BtnSaveSmtp_Click(object sender, RoutedEventArgs e)
    {
        if (TrySaveSmtpSettings())
        {
            ShowStatusText(TxtSmtpMessage, Loc.Instance["Settings_Smtp_Saved"], isError: false);
        }
    }

    private async void BtnTestSmtp_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySaveSmtpSettings())
        {
            return;
        }

        var recipient = TxtSmtpTestEmail.Text.Trim();
        if (recipient.Length == 0)
        {
            ShowStatusText(TxtSmtpMessage, Loc.Instance["Settings_Smtp_TestRequired"], isError: true);
            return;
        }

        var result = await _emailService.SendTestEmailAsync(recipient, Loc.Instance.IsEnglish);
        if (result.SentViaSmtp)
        {
            ShowStatusText(TxtSmtpMessage, Loc.Instance["Settings_Smtp_TestSent"], isError: false);
        }
        else if (result.SavedLocally && result.LocalFilePath is not null)
        {
            ShowStatusText(TxtSmtpMessage, Loc.Instance.Format("Settings_Smtp_TestSaved", result.LocalFilePath), isError: false);
        }
        else
        {
            ShowStatusText(TxtSmtpMessage, result.ErrorMessage ?? "Erreur SMTP", isError: true);
        }
    }

    private void BtnSaveMomo_Click(object sender, RoutedEventArgs e)
    {
        var current = _settingsStore.Load();
        current.MobileMoney.Provider = ChipMomoCampay.IsChecked == true ? "Campay" : "Simulation";
        current.MobileMoney.BaseUrl = string.IsNullOrWhiteSpace(TxtMomoBaseUrl.Text)
            ? "https://demo.campay.net/api"
            : TxtMomoBaseUrl.Text.Trim();
        current.MobileMoney.ApiToken = TxtMomoApiToken.Text.Trim();
        current.MobileMoney.Username = TxtMomoUsername.Text.Trim();
        current.MobileMoney.Password = TxtMomoPassword.Password;

        _settingsStore.Save(current);
        ShowStatusText(TxtMomoMessage, Loc.Instance["Settings_Momo_Saved"], isError: false);
    }

    private static void ShowStatusText(TextBlock target, string text, bool isError)
    {
        target.Text = text;
        target.Foreground = new SolidColorBrush(isError ? Color.FromRgb(192, 57, 43) : Color.FromRgb(30, 132, 73));
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
