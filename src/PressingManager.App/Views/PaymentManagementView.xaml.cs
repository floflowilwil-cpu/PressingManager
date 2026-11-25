
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PressingManager.App.Services;
using PressingManager.App.ViewModels;
using PressingManager.Core.DTOs;

namespace PressingManager.App.Views;

/// <summary>
/// Ecran WPF de gestion des paiements.
/// </summary>
public partial class PaymentManagementView : UserControl
{
    private readonly LanguageService _languageService;

    private readonly Dictionary<string, string> _translations = new()
    {
        ["Paiements, Caisse & Dépenses"] = "Payments, Cash Register & Expenses",
        ["Encaissement sécurisé, validation et génération de reçus"] =
            "Secure payment collection, validation and receipt generation",
        ["1. Commande à encaisser"] = "1. Order to be paid",
        ["Charger commande (ID)"] = "Load order (ID)",
        ["N° Commande"] = "Order No.",
        ["Client"] = "Customer",
        ["Total Facturé"] = "Total Invoiced",
        ["Reste à payer"] = "Remaining Balance",
        ["2. Moyen de paiement & Validation"] = "2. Payment Method & Validation",
        ["Espèces"] = "Cash",
        ["Autre"] = "Other",
        ["Montant à encaisser"] = "Amount to collect",
        ["Référence transaction Mobile Money"] = "Mobile Money transaction reference",
        ["Valider le paiement & Générer le reçu"] = "Confirm payment & Generate receipt",
        ["Dernier reçu prêt à imprimer"] = "Latest receipt ready to print",
        ["Voir / Imprimer le reçu"] = "View / Print receipt",
        ["3. Historique des paiements de la commande"] = "3. Order payment history",
        ["N°"] = "No.",
        ["Date"] = "Date",
        ["Montant"] = "Amount",
        ["Mode"] = "Method",
        ["Référence"] = "Reference",
        ["Statut"] = "Status",
        ["Motif"] = "Reason",
        ["Reçu"] = "Receipt",
        ["Annuler"] = "Cancel",
        ["Rembourser"] = "Refund",
        ["Gestion de la Caisse"] = "Cash Register Management",
        ["Fonds d'ouverture"] = "Opening Balance",
        ["Ouvrir la caisse"] = "Open Cash Register",
        ["Espèces comptées (clôture)"] = "Cash Counted (Closing)",
        ["Commentaire clôture"] = "Closing Comment",
        ["Clôturer la caisse"] = "Close Cash Register",
        ["Nouvelle Dépense"] = "New Expense",
        ["Catégorie (ex. Détergent, Électricité)"] = "Category (e.g. Detergent, Electricity)",
        ["Mode de règlement"] = "Payment Method",
        ["Commentaire"] = "Comment",
        ["Enregistrer la dépense"] = "Save Expense",
        ["Dépenses récentes (30 j)"] = "Recent Expenses (30 days)",
        ["Motif obligatoire pour annulation ou remboursement"] =
            "Reason required for cancellation or refund"
    };

    public PaymentManagementView(
        PaymentManagementViewModel viewModel,
        LanguageService languageService)
    {
        InitializeComponent();

        DataContext = viewModel
            ?? throw new ArgumentNullException(nameof(viewModel));

        _languageService = languageService
            ?? throw new ArgumentNullException(nameof(languageService));

        viewModel.ReceiptReady += OnReceiptReady;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _languageService.LanguageChanged += OnLanguageChanged;
        ApplyLanguage();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _languageService.LanguageChanged -= OnLanguageChanged;
    }
private void OnLanguageChanged(object? sender, EventArgs e)
{
    ApplyLanguage();

    if (DataContext is PaymentManagementViewModel viewModel)
    {
        viewModel.RefreshLanguage();
    }
}
    private void ApplyLanguage()
    {
        TranslateElements(this);
    }

    private void TranslateElements(DependencyObject parent)
    {
        if (parent is TextBlock textBlock)
        {
            // Ne pas modifier les textes provenant du ViewModel.
            if (textBlock.GetBindingExpression(TextBlock.TextProperty) == null)
            {
                textBlock.Text = TranslateText(textBlock.Text);
            }
        }

        if (parent is Button button && button.Content is string content)
        {
            button.Content = TranslateText(content);
        }

        if (parent is DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
                if (column.Header is string header)
                {
                    column.Header = TranslateText(header);
                }
            }
        }

        if (parent is FrameworkElement element &&
            element.ToolTip is string tooltip)
        {
            element.ToolTip = TranslateText(tooltip);
        }

        int count = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            TranslateElements(VisualTreeHelper.GetChild(parent, i));
        }
    }

    private string TranslateText(string text)
    {
        if (_languageService.IsEnglish)
        {
            return _translations.TryGetValue(text, out string? translation)
                ? translation
                : text;
        }

        foreach (var item in _translations)
        {
            if (item.Value == text)
                return item.Key;
        }

        return text;
    }

    private void OnReceiptReady(object? sender, PaymentReceiptDto receipt)
    {
        var window = new PaymentReceiptWindow(receipt)
        {
            Owner = Window.GetWindow(this)
        };

        window.ShowDialog();
    }
}
