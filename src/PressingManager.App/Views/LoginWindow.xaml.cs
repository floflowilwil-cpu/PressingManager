using System.Windows;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

/// <summary>
/// Utilisation : <c>new LoginWindow(vm).ShowDialog() == true</c> signifie "connecté".
/// Le PasswordBox WPF ne se lie pas directement : le code-behind recopie le texte dans le ViewModel.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.LoginSucceeded += (_, _) => DialogResult = true;

        Loaded += async (_, _) =>
        {
            await viewModel.InitializeAsync();
            UsernameBox.Focus();
        };
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.Password = PasswordBox.Password;

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.ConfirmPassword = ConfirmBox.Password;
}
