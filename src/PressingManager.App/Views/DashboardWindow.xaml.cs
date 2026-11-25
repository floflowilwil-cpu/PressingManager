using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PressingManager.App.Localization;
using PressingManager.App.ViewModels;
using PressingManager.Core.Common;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.Views;

public partial class DashboardWindow : Window
{
    private enum Section
    {
        Home,
        Customers,
        Orders,
        Services,
        Stock,
        Payments,
        Statistics,
        Users,
        Settings
    }

    /// <summary>Une ligne de la liste des alertes de stock (textes deja formates).</summary>
    public sealed record AlertRow(string Name, string Detail);

    private readonly CustomerManagementView _customerView;
    private readonly OrderProcessingView _orderProcessingView;
    private readonly ServiceCatalogView _serviceCatalogView;
    private readonly StockView _stockView;
    private readonly PaymentManagementView _paymentManagementView;
    private readonly ReportsView _reportsView;
    private readonly UserManagementView _userManagementView;
    private readonly SettingsView _settingsView;
    private readonly IDashboardService _dashboard;
    private readonly ICurrentUserContext _session;

    public DashboardWindow(
        CustomerManagementView customerView,
        OrderProcessingView orderProcessingView,
        ServiceCatalogView serviceCatalogView,
        StockView stockView,
        PaymentManagementView paymentManagementView,
        ReportsView reportsView,
        UserManagementView userManagementView,
        SettingsView settingsView,
        IDashboardService dashboard,
        ICurrentUserContext session)
    {
        InitializeComponent();
        SetNavigationMenu(false);

        _customerView = customerView ?? throw new ArgumentNullException(nameof(customerView));
        _orderProcessingView = orderProcessingView ?? throw new ArgumentNullException(nameof(orderProcessingView));
        _serviceCatalogView = serviceCatalogView ?? throw new ArgumentNullException(nameof(serviceCatalogView));
        _stockView = stockView ?? throw new ArgumentNullException(nameof(stockView));
        _paymentManagementView = paymentManagementView ?? throw new ArgumentNullException(nameof(paymentManagementView));
        _reportsView = reportsView ?? throw new ArgumentNullException(nameof(reportsView));
        _userManagementView = userManagementView ?? throw new ArgumentNullException(nameof(userManagementView));
        _settingsView = settingsView ?? throw new ArgumentNullException(nameof(settingsView));
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _session = session ?? throw new ArgumentNullException(nameof(session));

        CustomersHost.Content = _customerView;
        OrdersHost.Content = _orderProcessingView;
        ServicesHost.Content = _serviceCatalogView;
        StockHost.Content = _stockView;
        PaymentsHost.Content = _paymentManagementView;
        StatisticsHost.Content = _reportsView;
        UsersHost.Content = _userManagementView;
        SettingsHost.Content = _settingsView;

        _orderProcessingView.PayOrderRequested += async (_, request) =>
            await OpenPaymentForOrderAsync(request);

        // La fenetre est creee avant la connexion : on lit les droits au moment ou elle s'affiche.
        // Confort visuel seulement : le vrai controle d'acces est fait dans les services.
        Loaded += async (_, _) =>
        {
            ApplyPermissions();
            ShowUser();
            await RefreshHomeAsync();
        };

        Loc.Instance.LanguageChanged += async (_, _) =>
        {
            ShowUser();
            await RefreshHomeAsync();
        };
    }

    // ---------- Menu lateral escamotable ----------

    private void SetNavigationMenu(bool isOpen)
    {
        SidebarHost.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.Width = isOpen ? new GridLength(256) : new GridLength(0);
    }

    private void BtnToggleMenu_Click(object sender, RoutedEventArgs e)
    {
        SetNavigationMenu(SidebarHost.Visibility != Visibility.Visible);
    }

    // ---------- Droits et utilisateur ----------

    private void ApplyPermissions()
    {
        NavCustomers.Visibility = VisibleIf(Permission.ManageCustomers);
        QuickCustomers.Visibility = VisibleIf(Permission.ManageCustomers);
        NavOrders.Visibility = VisibleIf(Permission.ViewOrders);
        QuickOrders.Visibility = VisibleIf(Permission.ViewOrders);
        NavServices.Visibility = VisibleIf(Permission.ManageServices);
        NavStock.Visibility = VisibleIf(Permission.ViewStock);
        NavStatistics.Visibility = VisibleIf(Permission.ViewReports);
        NavUsers.Visibility = VisibleIf(Permission.ManageUsers);
    }

    private Visibility VisibleIf(Permission permission) =>
        _session.HasPermission(permission) ? Visibility.Visible : Visibility.Collapsed;

    private void ShowUser()
    {
        var user = _session.CurrentUser;
        if (user is null)
        {
            return;
        }

        TxtUserName.Text = user.FullName;
        TxtUserRole.Text = Loc.Instance.Role(user.Role);
        TxtUserInitial.Text = user.FullName.Trim().Length > 0
            ? user.FullName.Trim()[0].ToString().ToUpperInvariant()
            : "?";

        var firstName = user.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.FullName;
        TxtWelcome.Text = Loc.Instance.Format("Dash_Hello", firstName);
    }

    // ---------- Accueil : chiffres reels ----------

    private async Task RefreshHomeAsync()
    {
        TxtHomeMessage.Text = string.Empty;

        if (!_session.HasPermission(Permission.ViewDashboard))
        {
            TxtHomeMessage.Text = Loc.Instance["Dash_NoAccess"];
            return;
        }

        try
        {
            var summary = await _dashboard.GetSummaryAsync();

            TxtOrdersToday.Text = summary.OrdersToday.ToString();
            TxtInProgress.Text = summary.OrdersInProgress.ToString();
            TxtReadySub.Text = Loc.Instance.Format("Dash_Ready", summary.OrdersReady);
            TxtRevenueToday.Text = MoneyFormatter.FormatFcfa(summary.RevenueToday);
            TxtOutstanding.Text = MoneyFormatter.FormatFcfa(summary.OutstandingBalance);
            TxtUnpaidSub.Text = Loc.Instance.Format("Dash_Unpaid", summary.UnpaidOrderCount);

            var alerts = new List<AlertRow>();
            foreach (var alert in summary.StockAlerts)
            {
                alerts.Add(new AlertRow(
                    alert.ProductName,
                    $"{alert.CurrentStock:0.##} / {alert.AlertThreshold:0.##} {alert.Unit}"));
            }

            StockAlertsList.ItemsSource = alerts;
            TxtNoAlert.Visibility = alerts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            TxtHomeMessage.Text = ex.Message;
        }
    }

    private async void BtnRefreshHome_Click(object sender, RoutedEventArgs e) => await RefreshHomeAsync();

    // ---------- Navigation ----------

    private async void NavDashboard_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Home);
        await RefreshHomeAsync();
    }

    private async void NavCustomers_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Customers);
        await _customerView.RefreshAsync();
    }

    private async void NavOrders_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Orders);
        await _orderProcessingView.RefreshAsync();
    }

    private async void NavServices_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Services);
        await _serviceCatalogView.RefreshAsync();
    }

    private async void NavStock_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Stock);
        await _stockView.RefreshAsync();
    }

    private async void NavPayments_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Payments);

        if (_paymentManagementView.DataContext is PaymentManagementViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    private async Task OpenPaymentForOrderAsync(PayOrderRequest request)
    {
        SwitchSection(Section.Payments);

        if (_paymentManagementView.DataContext is PaymentManagementViewModel vm)
        {
            await vm.LoadOrderByIdAsync(request.OrderId, request.PreferredMethod);
        }
    }

    private async void NavStatistics_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Statistics);
        await _reportsView.RefreshAsync();
    }

    private async void NavUsers_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Users);
        await _userManagementView.RefreshAsync();
    }

    private async void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(Section.Settings);
        await _settingsView.RefreshAsync();
    }

    /// <summary>Change le module affiche et referme le menu apres la selection.</summary>
    private void SwitchSection(Section section)
    {
        SetNavigationMenu(false);
        HomeHost.Visibility = Show(section == Section.Home);
        CustomersHost.Visibility = Show(section == Section.Customers);
        OrdersHost.Visibility = Show(section == Section.Orders);
        ServicesHost.Visibility = Show(section == Section.Services);
        StockHost.Visibility = Show(section == Section.Stock);
        PaymentsHost.Visibility = Show(section == Section.Payments);
        StatisticsHost.Visibility = Show(section == Section.Statistics);
        UsersHost.Visibility = Show(section == Section.Users);
        SettingsHost.Visibility = Show(section == Section.Settings);

        // Les boutons « acces rapide » de l'accueil doivent aussi allumer le bon element du menu.
        NavDashboard.IsChecked = section == Section.Home;
        NavCustomers.IsChecked = section == Section.Customers;
        NavOrders.IsChecked = section == Section.Orders;
        NavServices.IsChecked = section == Section.Services;
        NavStock.IsChecked = section == Section.Stock;
        NavPayments.IsChecked = section == Section.Payments;
        NavStatistics.IsChecked = section == Section.Statistics;
        NavUsers.IsChecked = section == Section.Users;
        NavSettings.IsChecked = section == Section.Settings;
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
