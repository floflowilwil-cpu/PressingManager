
using System.Windows;
using System.Windows.Media;

namespace PressingManager.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Passer en français
        private void BtnFrench_Click(object sender, RoutedEventArgs e)
        {
            txtWelcome.Text = "Bienvenue !";
            txtSubtitle.Text = "Connectez-vous à votre espace de travail";
            txtUsernameLabel.Text = "Nom d'utilisateur";
            txtPasswordLabel.Text = "Mot de passe";
            chkRemember.Content = "Se souvenir de moi";
            btnLogin.Content = "➜   Se connecter";
            txtForgotPassword.Text = "Mot de passe oublié ?";

            btnFrench.Background = new SolidColorBrush(
                Color.FromRgb(8, 100, 217));

            btnFrench.Foreground = Brushes.White;

            btnEnglish.Background = Brushes.Transparent;
            btnEnglish.Foreground = new SolidColorBrush(
                Color.FromRgb(23, 74, 145));
        }

        // Passer en anglais
        private void BtnEnglish_Click(object sender, RoutedEventArgs e)
        {
            txtWelcome.Text = "Welcome!";
            txtSubtitle.Text = "Sign in to your workspace";
            txtUsernameLabel.Text = "Username";
            txtPasswordLabel.Text = "Password";
            chkRemember.Content = "Remember me";
            btnLogin.Content = "➜   Sign in";
            txtForgotPassword.Text = "Forgot password?";

            btnEnglish.Background = new SolidColorBrush(
                Color.FromRgb(8, 100, 217));

            btnEnglish.Foreground = Brushes.White;

            btnFrench.Background = Brushes.Transparent;
            btnFrench.Foreground = new SolidColorBrush(
                Color.FromRgb(23, 74, 145));
        }
    }
}
