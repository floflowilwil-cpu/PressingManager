using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PressingManager.App.Localization;
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

        Loc.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => Loc.Instance.LanguageChanged -= OnLanguageChanged;

        // La fenetre est creee avant la connexion : on lit les droits au moment ou elle s'affiche.
        // Confort visuel seulement : le vrai controle d'acces est fait dans les services.
        Loaded += (_, _) =>
        {
            BtnStatistics.Visibility = VisibleIf(session, Permission.ViewReports);
            BtnUsers.Visibility = VisibleIf(session, Permission.ManageUsers);
            UpdateLanguageButtons();
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

    private void BtnFrench_Click(object sender, RoutedEventArgs e) =>
        Loc.Instance.SetLanguage(AppLanguage.Fr);

    private void BtnEnglish_Click(object sender, RoutedEventArgs e) =>
        Loc.Instance.SetLanguage(AppLanguage.En);

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        UpdateLanguageButtons();

    private void UpdateLanguageButtons()
    {
        var activeBg = new SolidColorBrush(Color.FromRgb(8, 100, 217));
        var idleBg = new SolidColorBrush(Color.FromRgb(23, 74, 117));
        var idleFg = new SolidColorBrush(Color.FromRgb(214, 225, 239));
        var english = Loc.Instance.IsEnglish;

        BtnFrench.Background = english ? idleBg : activeBg;
        BtnFrench.Foreground = english ? idleFg : Brushes.White;
        BtnFrench.FontWeight = english ? FontWeights.Normal : FontWeights.Bold;

        BtnEnglish.Background = english ? activeBg : idleBg;
        BtnEnglish.Foreground = english ? Brushes.White : idleFg;
        BtnEnglish.FontWeight = english ? FontWeights.Bold : FontWeights.Normal;
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
