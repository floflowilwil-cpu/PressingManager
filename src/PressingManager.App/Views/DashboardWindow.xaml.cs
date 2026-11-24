using System;
using System.Windows;
using System.Windows.Controls;
using PressingManager.App.ViewModels;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.Views;

public partial class DashboardWindow : Window
{
    private enum Section
    {
        Home,
        Payments,
        Statistics,
        Users
    }

    private readonly PaymentManagementView _paymentManagementView;
    private readonly ReportsView _reportsView;
    private readonly UserManagementView _userManagementView;

    public DashboardWindow(
        PaymentManagementView paymentManagementView,
        ReportsView reportsView,
        UserManagementView userManagementView,
        ICurrentUserContext session)
    {
        InitializeComponent();

        _paymentManagementView = paymentManagementView ?? throw new ArgumentNullException(nameof(paymentManagementView));
        _reportsView = reportsView ?? throw new ArgumentNullException(nameof(reportsView));
        _userManagementView = userManagementView ?? throw new ArgumentNullException(nameof(userManagementView));

        PaymentsContentHost.Content = _paymentManagementView;
        StatisticsContentHost.Content = _reportsView;
        UsersContentHost.Content = _userManagementView;

        // La fenetre est creee avant la connexion : on lit les droits au moment ou elle s'affiche.
        // Confort visuel seulement : le vrai controle d'acces est fait dans les services.
        Loaded += (_, _) =>
        {
            BtnStatistics.Visibility = VisibleIf(session, Permission.ViewReports);
            BtnUsers.Visibility = VisibleIf(session, Permission.ManageUsers);
        };
    }

    private static Visibility VisibleIf(ICurrentUserContext session, Permission permission) =>
        session.HasPermission(permission) ? Visibility.Visible : Visibility.Collapsed;

    private void BtnDashboard_Click(object sender, RoutedEventArgs e) =>
        SwitchSection(Section.Home);

    private async void BtnPayments_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Payments);

        if (_paymentManagementView.DataContext is PaymentManagementViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    private async void BtnStatistics_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Statistics);
        await _reportsView.RefreshAsync();
    }

    private async void BtnUsers_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Users);
        await _userManagementView.RefreshAsync();
    }

    /// <summary>Seule la zone blanche change : le menu lateral reste toujours affiche.</summary>
    private void SwitchSection(Section section)
    {
        DashboardHomeContent.Visibility = section == Section.Home ? Visibility.Visible : Visibility.Collapsed;
        PaymentsContentHost.Visibility = section == Section.Payments ? Visibility.Visible : Visibility.Collapsed;
        StatisticsContentHost.Visibility = section == Section.Statistics ? Visibility.Visible : Visibility.Collapsed;
        UsersContentHost.Visibility = section == Section.Users ? Visibility.Visible : Visibility.Collapsed;

        SetNavState(BtnDashboard, section == Section.Home);
        SetNavState(BtnPayments, section == Section.Payments);
        SetNavState(BtnStatistics, section == Section.Statistics);
        SetNavState(BtnUsers, section == Section.Users);
    }

    private void SetNavState(Button button, bool isActive)
    {
        button.Style = (Style)FindResource(isActive ? "ActiveSidebarButton" : "SidebarButton");
    }
}
