using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PressingManager.App.ViewModels;
using PressingManager.App.Views;
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
        Users,
        Customers
    }

    private readonly PaymentManagementView _paymentManagementView;
    private readonly ReportsView _reportsView;
    private readonly UserManagementView _userManagementView;
    private readonly OrderProcessingView _orderProcessingView;
    private readonly ServiceCatalogView _serviceCatalogView;
    private readonly StockView _stockView;
    private readonly CustomerManagementView _customerManagementView;
    private readonly ICurrentUserContext _session;

    public DashboardWindow(
        PaymentManagementView paymentManagementView,
        ReportsView reportsView,
        UserManagementView userManagementView,
        OrderProcessingView orderProcessingView,
        ServiceCatalogView serviceCatalogView,
        StockView stockView,
        CustomerManagementView customerManagementView,
        ICurrentUserContext session)
    {
        InitializeComponent();

        _paymentManagementView = paymentManagementView ?? throw new ArgumentNullException(nameof(paymentManagementView));
        _reportsView = reportsView ?? throw new ArgumentNullException(nameof(reportsView));
        _userManagementView = userManagementView ?? throw new ArgumentNullException(nameof(userManagementView));
        _orderProcessingView = orderProcessingView ?? throw new ArgumentNullException(nameof(orderProcessingView));
        _serviceCatalogView = serviceCatalogView ?? throw new ArgumentNullException(nameof(serviceCatalogView));
        _stockView = stockView ?? throw new ArgumentNullException(nameof(stockView));
        _customerManagementView = customerManagementView ?? throw new ArgumentNullException(nameof(customerManagementView));
        _session = session ?? throw new ArgumentNullException(nameof(session));

        PaymentsContentHost.Content = _paymentManagementView;
        StatisticsContentHost.Content = _reportsView;
        UsersContentHost.Content = _userManagementView;
        ProcessingDashboardHost.Content = _orderProcessingView;
        ServicesDashboardHost.Content = _serviceCatalogView;
        StockDashboardHost.Content = _stockView;
        CustomersContentHost.Content = _customerManagementView;

        // La fenetre est creee avant la connexion : on lit les droits au moment ou elle s'affiche.
        // Confort visuel seulement : le vrai controle d'acces est fait dans les services.
        Loaded += (_, _) =>
        {
            BtnStatistics.Visibility = VisibleIf(session, Permission.ViewReports);
            BtnUsers.Visibility = VisibleIf(session, Permission.ManageUsers);
            ProcessingDashboardSection.Visibility = VisibleIf(_session, Permission.ViewOrders);
            ServicesDashboardSection.Visibility = VisibleIf(_session, Permission.ManageServices);
            StockDashboardSection.Visibility = VisibleIf(_session, Permission.ViewStock);
        };

        Loaded += async (_, _) =>
        {
            await RefreshPersonThreeModulesAsync();
        };
    }

    private static Visibility VisibleIf(ICurrentUserContext session, Permission permission) =>
        session.HasPermission(permission) ? Visibility.Visible : Visibility.Collapsed;

    private async void BtnDashboard_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Home);
        await RefreshPersonThreeModulesAsync();
    }

    private async void BtnCustomers_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Customers);
        await _customerManagementView.RefreshAsync();
    }

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

    private async Task RefreshPersonThreeModulesAsync()
    {
        if (_session.HasPermission(Permission.ViewOrders))
            await _orderProcessingView.RefreshAsync();

        if (_session.HasPermission(Permission.ManageServices))
            await _serviceCatalogView.RefreshAsync();

        if (_session.HasPermission(Permission.ViewStock))
            await _stockView.RefreshAsync();
    }

    /// <summary>Seule la zone blanche change : le menu lateral reste toujours affiche.</summary>
    private void SwitchSection(Section section)
    {
        DashboardHomeContent.Visibility = section == Section.Home ? Visibility.Visible : Visibility.Collapsed;
        PaymentsContentHost.Visibility = section == Section.Payments ? Visibility.Visible : Visibility.Collapsed;
        StatisticsContentHost.Visibility = section == Section.Statistics ? Visibility.Visible : Visibility.Collapsed;
        UsersContentHost.Visibility = section == Section.Users ? Visibility.Visible : Visibility.Collapsed;
        CustomersContentHost.Visibility = section == Section.Customers ? Visibility.Visible : Visibility.Collapsed;
        SetNavState(BtnDashboard, section == Section.Home);
        SetNavState(BtnPayments, section == Section.Payments);
        SetNavState(BtnStatistics, section == Section.Statistics);
        SetNavState(BtnUsers, section == Section.Users);
        SetNavState(BtnCustomers, section == Section.Customers);
    }

    private void SetNavState(Button button, bool isActive)
    {
        button.Style = (Style)FindResource(isActive ? "ActiveSidebarButton" : "SidebarButton");
    }
}