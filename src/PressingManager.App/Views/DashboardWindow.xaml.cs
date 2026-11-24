using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

public partial class DashboardWindow : Window
{
    private static readonly Brush ActiveBackground = new SolidColorBrush(Color.FromRgb(8, 100, 217));
    private static readonly Brush InactiveText = new SolidColorBrush(Color.FromRgb(214, 225, 239));

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

    private static void SetNavState(Button button, bool isActive)
    {
        button.Background = isActive ? ActiveBackground : Brushes.Transparent;
        button.Foreground = isActive ? Brushes.White : InactiveText;
        button.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
    }
}
