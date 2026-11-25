
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

public sealed record RoleOption(UserRole Value, string Label);

public sealed record UserRow(UserDto Source)
{
    public int Id => Source.Id;
    public string FullName => Source.FullName;
    public string Username => Source.Username;
    public string RoleText => Loc.Instance.Role(Source.Role);
    public bool IsActive => Source.IsActive;
    public DateTime? LastLoginLocal => Source.LastLoginLocal;
}

public partial class UserManagementViewModel : ObservableObject
{
    private readonly IUserService _users;
    private readonly ICurrentUserContext _session;

    private IReadOnlyList<UserDto> _all = Array.Empty<UserDto>();

    public UserManagementViewModel(
        IUserService users,
        ICurrentUserContext session)
    {
        _users = users;
        _session = session;

        _roles = BuildRoles();

        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<UserRow> Users { get; } = new();

    public bool CanManageUsers =>
        _session.HasPermission(Permission.ManageUsers);

    public bool IsAccessDenied => !CanManageUsers;

    public void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanManageUsers));
        OnPropertyChanged(nameof(IsAccessDenied));
    }

    [ObservableProperty]
    private IReadOnlyList<RoleOption> _roles;

    [ObservableProperty]
    private UserRow? _selectedUser;

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
    private string? _recoveryCode;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isError;

    partial void OnSelectedUserChanged(UserRow? value)
    {
        RecoveryCode = null;
        Message = null;
    }

    [RelayCommand]
    private Task LoadAsync() =>
        RunAsync(RefreshAsync, successKey: null);

    [RelayCommand]
    private Task CreateAsync() => RunAsync(async () =>
    {
        await _users.CreateAsync(new CreateUserDto(
            NewFullName,
            NewUsername,
            NewPassword,
            NewRole));

        NewFullName = string.Empty;
        NewUsername = string.Empty;
        NewPassword = string.Empty;

        await RefreshAsync();
    }, "Users_Created");

    [RelayCommand]
    private Task ToggleActiveAsync() => RunAsync(async () =>
    {
        var user = RequireSelection();

        await _users.SetActiveAsync(user.Id, !user.IsActive);

        await RefreshAsync(user.Id);
    }, "Users_StatusUpdated");

    [RelayCommand]
    private Task ResetPasswordAsync() => RunAsync(async () =>
    {
        var user = RequireSelection();

        await _users.ResetPasswordAsync(
            user.Id,
            ResetPasswordValue);

        ResetPasswordValue = string.Empty;
    }, "Users_PasswordReset");

    [RelayCommand]
    private Task GenerateRecoveryCodeAsync() => RunAsync(async () =>
    {
        var user = RequireSelection();

        RecoveryCode =
            await _users.GenerateRecoveryCodeAsync(user.Id);

        Message = Loc.Instance.Format(
            "Users_RecoveryShown",
            user.FullName,
            RecoveryCode);

        IsError = false;
    }, successKey: null);

    private UserRow RequireSelection() =>
        SelectedUser ?? throw new BusinessRuleException(
            Loc.Instance["Users_SelectFirst"]);

    private async Task RefreshAsync() =>
        await RefreshAsync(SelectedUser?.Id);

    private async Task RefreshAsync(int? keepSelectionId)
    {
        _all = await _users.GetAllAsync();
        RebuildRows(keepSelectionId);
    }

    private void RebuildRows(int? keepSelectionId)
    {
        Users.Clear();

        foreach (var user in _all)
        {
            Users.Add(new UserRow(user));
        }

        SelectedUser = Users.FirstOrDefault(
            user => user.Id == keepSelectionId);
    }

    private static List<RoleOption> BuildRoles() =>
        Enum.GetValues<UserRole>()
            .Select(role => new RoleOption(
                role,
                Loc.Instance.Role(role)))
            .ToList();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Roles = BuildRoles();

        OnPropertyChanged(nameof(NewRole));

        RebuildRows(SelectedUser?.Id);
    }

    private async Task RunAsync(
        Func<Task> action,
        string? successKey,
        bool keepMessage = false)
    {
        if (!keepMessage)
        {
            Message = null;
        }

        try
        {
            await action();

            if (successKey is not null)
            {
                IsError = false;
                Message = Loc.Instance[successKey];
            }
        }
        catch (Exception ex) when (
            ex is BusinessRuleException or
            PermissionDeniedException or
            InvalidOperationException)
        {
            IsError = true;
            Message = ex.Message;
        }
    }
}
