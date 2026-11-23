
using System.Windows;
using System.Windows.Media;
using PressingManager.App.ViewModels;

namespace PressingManager.App
{
    public partial class MainWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public MainWindow(LoginViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;

            DataContext = _viewModel;

            _viewModel.LoginSucceeded += OnLoginSucceeded;
            
            Loaded += async (_, _) => await _viewModel.InitializeAsync();
        }

        private void OnLoginSucceeded(object? sender, EventArgs e)
        {
            MessageBox.Show(
                "Connexion réussie !",
                "Pressing Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Username = txtUsername.Text.Trim();
            _viewModel.Password = txtPassword.Password;

            await _viewModel.SubmitCommand.ExecuteAsync(null);
        }

        private void BtnFrench_Click(object sender, RoutedEventArgs e)
        {
            txtWelcome.Text = "Bienvenue !";
            txtSubtitle.Text = "Connectez-vous à votre espace de travail";
            txtUsernameLabel.Text = "Nom d'utilisateur";
            txtPasswordLabel.Text = "Mot de passe";
            chkRemember.Content = "Se souvenir de moi";
            btnLogin.Content = "➜   Se connecter";
            txtForgotPassword.Text = "Mot de passe oublié ?";

            btnFrench.Background = new SolidColorBrush(Color.FromRgb(8, 100, 217));
            btnFrench.Foreground = Brushes.White;
            btnEnglish.Background = Brushes.Transparent;
            btnEnglish.Foreground = new SolidColorBrush(Color.FromRgb(23, 74, 145));
        }

        private void BtnEnglish_Click(object sender, RoutedEventArgs e)
        {
            txtWelcome.Text = "Welcome!";
            txtSubtitle.Text = "Sign in to your workspace";
            txtUsernameLabel.Text = "Username";
            txtPasswordLabel.Text = "Password";
            chkRemember.Content = "Remember me";
            btnLogin.Content = "➜   Sign in";
            txtForgotPassword.Text = "Forgot password?";

            btnEnglish.Background = new SolidColorBrush(Color.FromRgb(8, 100, 217));
            btnEnglish.Foreground = Brushes.White;
            btnFrench.Background = Brushes.Transparent;
            btnFrench.Foreground = new SolidColorBrush(Color.FromRgb(23, 74, 145));
        }
    }
}
