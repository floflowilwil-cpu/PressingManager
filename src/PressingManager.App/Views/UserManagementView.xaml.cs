using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

/// <summary>Reserve a l'administrateur (permission ManageUsers, verifiee aussi dans UserService).</summary>
public partial class UserManagementView : UserControl
{
    private readonly UserManagementViewModel _viewModel;

    public UserManagementView(UserManagementViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        // Vide les PasswordBox quand le ViewModel remet les valeurs a zero.
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += async (_, _) => await viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UserManagementViewModel.NewPassword) && _viewModel.NewPassword.Length == 0)
        {
            NewPasswordBox.Clear();
        }
        else if (e.PropertyName == nameof(UserManagementViewModel.ResetPasswordValue) && _viewModel.ResetPasswordValue.Length == 0)
        {
            ResetPasswordBox.Clear();
        }
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.NewPassword = NewPasswordBox.Password;

    private void ResetPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.ResetPasswordValue = ResetPasswordBox.Password;
}
