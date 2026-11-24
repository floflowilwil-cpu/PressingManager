
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using PressingManager.App.ViewModels;
using PressingManager.App.Views;
using PressingManager.App.Services;

namespace PressingManager.App
{
    public partial class MainWindow : Window
    {
        private readonly LanguageService _languageService;
        private readonly LoginViewModel _viewModel;
        private readonly DashboardWindow _dashboardWindow;

       public MainWindow(
    LoginViewModel viewModel,
    DashboardWindow dashboardWindow,
    LanguageService languageService)
{
    InitializeComponent();

    _viewModel = viewModel;
    _dashboardWindow = dashboardWindow;
    _languageService = languageService;

    DataContext = _viewModel;

    _viewModel.LoginSucceeded += OnLoginSucceeded;

    Loaded += async (_, _) =>
    {
        await _viewModel.InitializeAsync();
    };
}

        // ACTUALISATION DU SOUS-TITRE
        private void RefreshSubtitle()
        {
            txtSubtitle.GetBindingExpression(TextBlock.TextProperty)
                ?.UpdateTarget();
        }

        // CONNEXION REUSSIE
        private void OnLoginSucceeded(object? sender, EventArgs e)
        {
            Application.Current.MainWindow = _dashboardWindow;

            _dashboardWindow.Show();

            Close();
        }

        // BOUTON DE CONNEXION
        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Username = txtUsername.Text.Trim();

            _viewModel.Password =
                txtPasswordVisible.Visibility == Visibility.Visible
                    ? txtPasswordVisible.Text
                    : txtPassword.Password;

            _viewModel.ConfirmPassword =
                txtConfirmPasswordVisible.Visibility == Visibility.Visible
                    ? txtConfirmPasswordVisible.Text
                    : txtConfirmPassword.Password;

            await _viewModel.SubmitCommand.ExecuteAsync(null);
        }

        // MOT DE PASSE : SYNCHRONISATION
        private void TxtPassword_PasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            _viewModel.Password = txtPassword.Password;
        }

        private void TxtPasswordVisible_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            txtPassword.Password = txtPasswordVisible.Text;
            _viewModel.Password = txtPasswordVisible.Text;
        }

        // AFFICHER / MASQUER LE MOT DE PASSE
        private void BtnTogglePassword_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (txtPasswordVisible.Visibility == Visibility.Collapsed)
            {
                txtPasswordVisible.Text = txtPassword.Password;

                txtPassword.Visibility = Visibility.Collapsed;
                txtPasswordVisible.Visibility = Visibility.Visible;

                btnTogglePassword.Content = "\uE8A7";
                btnTogglePassword.ToolTip = "Masquer le mot de passe";

                txtPasswordVisible.Focus();
                txtPasswordVisible.CaretIndex =
                    txtPasswordVisible.Text.Length;
            }
            else
            {
                txtPassword.Password = txtPasswordVisible.Text;

                txtPasswordVisible.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Visible;

                btnTogglePassword.Content = "\uE890";
                btnTogglePassword.ToolTip = "Afficher le mot de passe";

                txtPassword.Focus();
            }
        }

        // CONFIRMATION DU MOT DE PASSE : SYNCHRONISATION
        private void TxtConfirmPassword_PasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            _viewModel.ConfirmPassword = txtConfirmPassword.Password;
        }

        private void TxtConfirmPasswordVisible_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            txtConfirmPassword.Password =
                txtConfirmPasswordVisible.Text;

            _viewModel.ConfirmPassword =
                txtConfirmPasswordVisible.Text;
        }

        // AFFICHER / MASQUER LA CONFIRMATION
        private void BtnToggleConfirmPassword_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (txtConfirmPasswordVisible.Visibility ==
                Visibility.Collapsed)
            {
                txtConfirmPasswordVisible.Text =
                    txtConfirmPassword.Password;

                txtConfirmPassword.Visibility = Visibility.Collapsed;
                txtConfirmPasswordVisible.Visibility = Visibility.Visible;

                btnToggleConfirmPassword.Content = "\uE8A7";
                btnToggleConfirmPassword.ToolTip =
                    "Masquer le mot de passe";

                txtConfirmPasswordVisible.Focus();
                txtConfirmPasswordVisible.CaretIndex =
                    txtConfirmPasswordVisible.Text.Length;
            }
            else
            {
                txtConfirmPassword.Password =
                    txtConfirmPasswordVisible.Text;

                txtConfirmPasswordVisible.Visibility =
                    Visibility.Collapsed;

                txtConfirmPassword.Visibility = Visibility.Visible;

                btnToggleConfirmPassword.Content = "\uE890";
                btnToggleConfirmPassword.ToolTip =
                    "Afficher le mot de passe";

                txtConfirmPassword.Focus();
            }
        }

        // LANGUE FRANCAISE// LANGUE FRANCAISE
private void BtnFrench_Click(object sender, RoutedEventArgs e)
{
    _languageService.SetLanguage(false);

    _viewModel.IsEnglish = false;
    RefreshSubtitle();

    txtWelcome.Text = "Bienvenue !";
    txtUsernameLabel.Text = "Nom d'utilisateur";
    txtPasswordLabel.Text = "Mot de passe";
    chkRemember.Content = "Se souvenir de moi";
    txtForgotPassword.Text = "Mot de passe oublié ?";

    btnFrench.Background =
        new SolidColorBrush(Color.FromRgb(8, 100, 217));

    btnFrench.Foreground = Brushes.White;

    btnEnglish.Background = Brushes.Transparent;

    btnEnglish.Foreground =
        new SolidColorBrush(Color.FromRgb(23, 74, 145));
}

// LANGUE ANGLAISE
private void BtnEnglish_Click(object sender, RoutedEventArgs e)
{
    _languageService.SetLanguage(true);

    _viewModel.IsEnglish = true;
    RefreshSubtitle();

    txtWelcome.Text = "Welcome!";
    txtUsernameLabel.Text = "Username";
    txtPasswordLabel.Text = "Password";
    chkRemember.Content = "Remember me";
    txtForgotPassword.Text = "Forgot password?";

    btnEnglish.Background =
        new SolidColorBrush(Color.FromRgb(8, 100, 217));

    btnEnglish.Foreground = Brushes.White;

    btnFrench.Background = Brushes.Transparent;

    btnFrench.Foreground =
        new SolidColorBrush(Color.FromRgb(23, 74, 145));
}
    }
}
