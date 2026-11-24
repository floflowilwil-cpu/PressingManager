
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using PressingManager.App.Localization;
using PressingManager.App.ViewModels;
using PressingManager.App.Views;

namespace PressingManager.App
{
    public partial class MainWindow : Window
    {
        private readonly LoginViewModel _viewModel;
        private readonly DashboardWindow _dashboardWindow;

        public MainWindow(
            LoginViewModel viewModel,
            DashboardWindow dashboardWindow)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _dashboardWindow = dashboardWindow;

            DataContext = _viewModel;

            _viewModel.LoginSucceeded += OnLoginSucceeded;

            Loaded += async (_, _) =>
            {
                UpdateLanguageButtons();
                await _viewModel.InitializeAsync();
            };
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
                btnTogglePassword.ToolTip = Loc.Instance["Login_HidePassword"];

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
                btnTogglePassword.ToolTip = Loc.Instance["Login_ShowPassword"];

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
                    Loc.Instance["Login_HidePassword"];

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
                    Loc.Instance["Login_ShowPassword"];

                txtConfirmPassword.Focus();
            }
        }

        // LANGUES : les textes se mettent a jour tout seuls (voir Localization/Loc.cs)
        private void BtnFrench_Click(object sender, RoutedEventArgs e)
        {
            Loc.Instance.SetLanguage(AppLanguage.Fr);
            UpdateLanguageButtons();
        }

        private void BtnEnglish_Click(object sender, RoutedEventArgs e)
        {
            Loc.Instance.SetLanguage(AppLanguage.En);
            UpdateLanguageButtons();
        }

        private void UpdateLanguageButtons()
        {
            var selectedBackground = new SolidColorBrush(Color.FromRgb(8, 100, 217));
            var idleForeground = new SolidColorBrush(Color.FromRgb(23, 74, 145));
            var english = Loc.Instance.IsEnglish;

            btnFrench.Background = english ? Brushes.Transparent : selectedBackground;
            btnFrench.Foreground = english ? idleForeground : Brushes.White;

            btnEnglish.Background = english ? selectedBackground : Brushes.Transparent;
            btnEnglish.Foreground = english ? Brushes.White : idleForeground;
        }
    }
}
