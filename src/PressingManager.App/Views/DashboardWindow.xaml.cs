using System;
using System.Windows;
using System.Windows.Controls;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

public partial class DashboardWindow : Window
{
    private readonly PaymentManagementView _paymentManagementView;

    public DashboardWindow(PaymentManagementView paymentManagementView)
    {
        InitializeComponent();

        _paymentManagementView = paymentManagementView ?? throw new ArgumentNullException(nameof(paymentManagementView));
        PaymentsContentHost.Content = _paymentManagementView;
    }

    private void BtnDashboard_Click(object sender, RoutedEventArgs e) =>
        SwitchSection(showPayments: false);

    private async void BtnPayments_Click(object sender, RoutedEventArgs e)
    {
        SwitchSection(showPayments: true);

        if (_paymentManagementView.DataContext is PaymentManagementViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    private void SwitchSection(bool showPayments)
    {
        DashboardHomeContent.Visibility = showPayments ? Visibility.Collapsed : Visibility.Visible;
        PaymentsContentHost.Visibility = showPayments ? Visibility.Visible : Visibility.Collapsed;

        SetNavState(BtnDashboard, !showPayments);
        SetNavState(BtnPayments, showPayments);
    }

    private void SetNavState(Button button, bool isActive)
    {
        button.Style = (Style)FindResource(isActive ? "ActiveSidebarButton" : "SidebarButton");
    }
}
