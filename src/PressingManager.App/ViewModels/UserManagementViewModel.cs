using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

public sealed record RoleOption(UserRole Value, string Label);

public partial class UserManagementViewModel : ObservableObject
{
    private readonly IUserService _users;

    public UserManagementViewModel(IUserService users)
    {
        _users = users;
    }

    public ObservableCollection<UserDto> Users { get; } = new();

    public IReadOnlyList<RoleOption> Roles { get; } = Enum.GetValues<UserRole>()
        .Select(role => new RoleOption(role, UserRoleLabels.ToFrench(role)))
        .ToList();

    [ObservableProperty]
    private UserDto? _selectedUser;

    [ObservableProperty]
    private string _newFullName = string.Empty;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private UserRole _newRole = UserRole.FrontDesk;

    [ObservableProperty]
    private string _resetPasswordValue = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isError;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(RefreshAsync, successMessage: null);

    [RelayCommand]
    private Task CreateAsync() => RunAsync(async () =>
    {
        await _users.CreateAsync(new CreateUserDto(NewFullName, NewUsername, NewPassword, NewRole));

        NewFullName = string.Empty;
        NewUsername = string.Empty;
        NewPassword = string.Empty;
        await RefreshAsync();
    }, "Compte créé.");

    [RelayCommand]
    private Task ToggleActiveAsync() => RunAsync(async () =>
    {
        var user = RequireSelection();
        await _users.SetActiveAsync(user.Id, !user.IsActive);
        await RefreshAsync(user.Id);
    }, "Statut du compte mis à jour.");

    [RelayCommand]
    private Task ResetPasswordAsync() => RunAsync(async () =>
    {
        var user = RequireSelection();
        await _users.ResetPasswordAsync(user.Id, ResetPasswordValue);
        ResetPasswordValue = string.Empty;
    }, "Mot de passe réinitialisé.");

    private UserDto RequireSelection() =>
        SelectedUser ?? throw new BusinessRuleException("Sélectionnez d'abord un utilisateur dans la liste.");

    private async Task RefreshAsync() => await RefreshAsync(SelectedUser?.Id);

    private async Task RefreshAsync(int? keepSelectionId)
    {
        var all = await _users.GetAllAsync();

        Users.Clear();
        foreach (var user in all)
        {
            Users.Add(user);
        }

        SelectedUser = Users.FirstOrDefault(u => u.Id == keepSelectionId);
    }

    private async Task RunAsync(Func<Task> action, string? successMessage)
    {
        try
        {
            await action();
            IsError = false;
            Message = successMessage;
        }
        catch (Exception ex) when (ex is BusinessRuleException or PermissionDeniedException)
        {
            IsError = true;
            Message = ex.Message;
        }
    }
}
