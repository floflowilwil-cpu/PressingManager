using System;
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
        Processing,
        Services,
        Stock
    }

    private readonly PaymentManagementView _paymentManagementView;
    private readonly ReportsView _reportsView;
    private readonly UserManagementView _userManagementView;
    private readonly OrderProcessingView _orderProcessingView;
    private readonly ServiceCatalogView _serviceCatalogView;
    private readonly StockView _stockView;

    public DashboardWindow(
        PaymentManagementView paymentManagementView,
        ReportsView reportsView,
        UserManagementView userManagementView,
        OrderProcessingView orderProcessingView,
        ServiceCatalogView serviceCatalogView,
        StockView stockView,
        ICurrentUserContext session)
    {
        InitializeComponent();

        _paymentManagementView = paymentManagementView ?? throw new ArgumentNullException(nameof(paymentManagementView));
        _reportsView = reportsView ?? throw new ArgumentNullException(nameof(reportsView));
        _userManagementView = userManagementView ?? throw new ArgumentNullException(nameof(userManagementView));
        _orderProcessingView = orderProcessingView ?? throw new ArgumentNullException(nameof(orderProcessingView));
        _serviceCatalogView = serviceCatalogView ?? throw new ArgumentNullException(nameof(serviceCatalogView));
        _stockView = stockView ?? throw new ArgumentNullException(nameof(stockView));

        PaymentsContentHost.Content = _paymentManagementView;
        StatisticsContentHost.Content = _reportsView;
        UsersContentHost.Content = _userManagementView;
        ProcessingContentHost.Content = _orderProcessingView;
        ServicesContentHost.Content = _serviceCatalogView;
        StockContentHost.Content = _stockView;

        // La fenetre est creee avant la connexion : on lit les droits au moment ou elle s'affiche.
        // Confort visuel seulement : le vrai controle d'acces est fait dans les services.
        Loaded += (_, _) =>
        {
            BtnStatistics.Visibility = VisibleIf(session, Permission.ViewReports);
            BtnUsers.Visibility = VisibleIf(session, Permission.ManageUsers);
            BtnProcessing.Visibility = VisibleIf(session, Permission.ViewOrders);
            BtnServices.Visibility = VisibleIf(session, Permission.ManageServices);
            BtnStock.Visibility = VisibleIf(session, Permission.ViewStock);
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

    private async void BtnProcessing_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Processing);
        await _orderProcessingView.RefreshAsync();
    }

    private async void BtnServices_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Services);
        await _serviceCatalogView.RefreshAsync();
    }

    private async void BtnStock_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Stock);
        await _stockView.RefreshAsync();
    }

    /// <summary>Seule la zone blanche change : le menu lateral reste toujours affiche.</summary>
    private void SwitchSection(Section section)
    {
        DashboardHomeContent.Visibility = section == Section.Home ? Visibility.Visible : Visibility.Collapsed;
        PaymentsContentHost.Visibility = section == Section.Payments ? Visibility.Visible : Visibility.Collapsed;
        StatisticsContentHost.Visibility = section == Section.Statistics ? Visibility.Visible : Visibility.Collapsed;
        UsersContentHost.Visibility = section == Section.Users ? Visibility.Visible : Visibility.Collapsed;
        ProcessingContentHost.Visibility = section == Section.Processing ? Visibility.Visible : Visibility.Collapsed;
        ServicesContentHost.Visibility = section == Section.Services ? Visibility.Visible : Visibility.Collapsed;
        StockContentHost.Visibility = section == Section.Stock ? Visibility.Visible : Visibility.Collapsed;

        SetNavState(BtnDashboard, section == Section.Home);
        SetNavState(BtnPayments, section == Section.Payments);
        SetNavState(BtnStatistics, section == Section.Statistics);
        SetNavState(BtnUsers, section == Section.Users);
        SetNavState(BtnProcessing, section == Section.Processing);
        SetNavState(BtnServices, section == Section.Services);
        SetNavState(BtnStock, section == Section.Stock);
    }

    private void SetNavState(Button button, bool isActive)
    {
        button.Style = (Style)FindResource(isActive ? "ActiveSidebarButton" : "SidebarButton");
    }
}
