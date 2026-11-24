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
    }

    /// <summary>Recharge la liste (appele a chaque ouverture de l'onglet ; sans effet sans le droit ManageUsers).</summary>
    public Task RefreshAsync() =>
        _viewModel.CanManageUsers ? _viewModel.LoadCommand.ExecuteAsync(null) : Task.CompletedTask;

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
