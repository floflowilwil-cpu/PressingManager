
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PressingManager.App.Services;
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
        Statistics
    }

    private readonly PaymentManagementView _paymentManagementView;
    private readonly ReportsView _reportsView;
    private readonly LanguageService _languageService;

    private readonly Dictionary<string, string> _translations = new()
    {
        ["Tableau de bord"] = "Dashboard",
        ["Clients"] = "Customers",
        ["Commandes"] = "Orders",
        ["Vêtements"] = "Garments",
        ["Paiements"] = "Payments",
        ["Statistiques"] = "Statistics",
        ["Utilisateurs"] = "Users",
        ["GESTION PROFESSIONNELLE"] = "PROFESSIONAL MANAGEMENT",
        ["© 2026 Tous droits réservés"] = "© 2026 All rights reserved",
        ["MENU PRINCIPAL"] = "MAIN MENU",
        ["Bonjour et bienvenue !"] = "Hello and welcome!",
        ["Voici un aperçu de l'activité de votre pressing."] =
            "Here is an overview of your dry-cleaning business.",
        ["Vue d'ensemble"] = "Overview",
        ["CLIENTS"] = "CUSTOMERS",
        ["Total des clients"] = "Total customers",
        ["COMMANDES"] = "ORDERS",
        ["Total des commandes"] = "Total orders",
        ["EN COURS"] = "IN PROGRESS",
        ["Commandes en traitement"] = "Orders being processed",
        ["REVENUS"] = "REVENUE",
        ["Revenus enregistrés"] = "Recorded revenue",
        ["Activités récentes"] = "Recent activities",
        ["Suivez les dernières opérations effectuées."] =
            "Follow the latest operations performed.",
        ["Aucune activité récente"] = "No recent activity",
        ["Les dernières opérations apparaîtront ici."] =
            "The latest operations will appear here.",
        ["Suivi des commandes"] = "Order tracking",
        ["État général du traitement"] = "General processing status",
        ["En attente"] = "Pending",
        ["En traitement"] = "Processing",
        ["Terminées"] = "Completed",
        ["— commande(s)"] = "— order(s)"
    };

    public DashboardWindow(
        PaymentManagementView paymentManagementView,
        ReportsView reportsView,
        ICurrentUserContext session,
        LanguageService languageService)
    {
        InitializeComponent();

        _paymentManagementView = paymentManagementView
            ?? throw new ArgumentNullException(nameof(paymentManagementView));

        _reportsView = reportsView
            ?? throw new ArgumentNullException(nameof(reportsView));

        _languageService = languageService
            ?? throw new ArgumentNullException(nameof(languageService));

        PaymentsContentHost.Content = _paymentManagementView;
        StatisticsContentHost.Content = _reportsView;

        _languageService.LanguageChanged += OnLanguageChanged;

        Loaded += (_, _) =>
        {
            BtnStatistics.Visibility =
                session.HasPermission(Permission.ViewReports)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ApplyLanguage();
        };

        Closed += (_, _) =>
        {
            _languageService.LanguageChanged -= OnLanguageChanged;
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLanguage();
    }

    /// <summary>
    /// Applique les traductions à tous les textes du tableau de bord.
    /// </summary>
    private void ApplyLanguage()
    {
        Title = _languageService.Translate(
            "Pressing Manager - Tableau de bord",
            "Pressing Manager - Dashboard");

        TranslateElements(this);
    }

    private void TranslateElements(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            DependencyObject child =
                VisualTreeHelper.GetChild(parent, i);

            if (child is TextBlock textBlock)
            {
                string currentText = textBlock.Text;

                if (_translations.TryGetValue(currentText, out string? english))
                {
                    textBlock.Text = _languageService.IsEnglish
                        ? english
                        : currentText;
                }
                else
                {
                    foreach (var translation in _translations)
                    {
                        if (translation.Value == currentText)
                        {
                            textBlock.Text = _languageService.IsEnglish
                                ? currentText
                                : translation.Key;

                            break;
                        }
                    }
                }
            }

            TranslateElements(child);
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

    private void SwitchSection(Section section)
    {
        DashboardHomeContent.Visibility =
            section == Section.Home ? Visibility.Visible : Visibility.Collapsed;

        PaymentsContentHost.Visibility =
            section == Section.Payments ? Visibility.Visible : Visibility.Collapsed;

        StatisticsContentHost.Visibility =
            section == Section.Statistics ? Visibility.Visible : Visibility.Collapsed;

        SetNavState(BtnDashboard, section == Section.Home);
        SetNavState(BtnPayments, section == Section.Payments);
        SetNavState(BtnStatistics, section == Section.Statistics);
    }

    private void SetNavState(Button button, bool isActive)
    {
        button.Style = (Style)FindResource(
            isActive ? "ActiveSidebarButton" : "SidebarButton");
    }
}
