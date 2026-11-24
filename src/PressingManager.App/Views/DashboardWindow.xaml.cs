
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PressingManager.App.ViewModels;
using PressingManager.App.Views;
using PressingManager.Core.Common;
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
    private readonly IDashboardService _dashboardService;

    public DashboardWindow(
        PaymentManagementView paymentManagementView,
        ReportsView reportsView,
        UserManagementView userManagementView,
        IDashboardService dashboardService,
        ICurrentUserContext session)
    {
        InitializeComponent();

        _paymentManagementView = paymentManagementView
            ?? throw new ArgumentNullException(nameof(paymentManagementView));

        _reportsView = reportsView
            ?? throw new ArgumentNullException(nameof(reportsView));

        _userManagementView = userManagementView
            ?? throw new ArgumentNullException(nameof(userManagementView));

        _dashboardService = dashboardService
            ?? throw new ArgumentNullException(nameof(dashboardService));

        PaymentsContentHost.Content = _paymentManagementView;
        StatisticsContentHost.Content = _reportsView;
        UsersContentHost.Content = _userManagementView;

        // Les droits sont vérifiés lorsque la fenêtre s'affiche.
        Loaded += async (_, _) =>
        {
            BtnStatistics.Visibility = VisibleIf(session, Permission.ViewReports);
            BtnUsers.Visibility = VisibleIf(session, Permission.ManageUsers);

            await LoadDashboardSummaryAsync();
        };
    }

    private static Visibility VisibleIf(
        ICurrentUserContext session,
        Permission permission) =>
        session.HasPermission(permission)
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <summary>
    /// Charge les statistiques et actualise les quatre cartes du tableau de bord.
    /// </summary>
    private async Task LoadDashboardSummaryAsync()
    {
        try
        {
            var summary = await _dashboardService.GetSummaryAsync();
            var culture = CultureInfo.GetCultureInfo("fr-FR");

            TxtCustomerCount.Text =
                summary.ActiveCustomerCount.ToString("N0", culture);

            TxtOrderCount.Text =
                summary.TotalOrderCount.ToString("N0", culture);

            TxtOrdersInProgress.Text =
                summary.OrdersInProgress.ToString("N0", culture);

            TxtRevenueToday.Text =
                $"{summary.RevenueToday.ToString("N0", culture)} FCFA";
        }
        catch (PermissionDeniedException)
        {
            MessageBox.Show(
                "Vous ne disposez pas des droits nécessaires pour consulter les statistiques du tableau de bord.",
                "Accès refusé",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible de charger les statistiques du tableau de bord :\n{ex.Message}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

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

    /// <summary>
    /// Seule la zone blanche change : le menu latéral reste toujours affiché.
    /// </summary>
    private void SwitchSection(Section section)
    {
        DashboardHomeContent.Visibility =
            section == Section.Home ? Visibility.Visible : Visibility.Collapsed;

        PaymentsContentHost.Visibility =
            section == Section.Payments ? Visibility.Visible : Visibility.Collapsed;

        StatisticsContentHost.Visibility =
            section == Section.Statistics ? Visibility.Visible : Visibility.Collapsed;

        UsersContentHost.Visibility =
            section == Section.Users ? Visibility.Visible : Visibility.Collapsed;

        SetNavState(BtnDashboard, section == Section.Home);
        SetNavState(BtnPayments, section == Section.Payments);
        SetNavState(BtnStatistics, section == Section.Statistics);
        SetNavState(BtnUsers, section == Section.Users);
    }

    private void SetNavState(Button button, bool isActive)
    {
        button.Style = (Style)FindResource(
            isActive ? "ActiveSidebarButton" : "SidebarButton");
    }
}
