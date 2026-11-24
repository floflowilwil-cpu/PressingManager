using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;

namespace PressingManager.App.Views;

/// <summary>
/// Fenetre de confirmation de paiement reussi avec apercu stylise du recu,
/// impression directe (ou PDF via Microsoft Print to PDF) et export texte.
/// </summary>
public partial class PaymentReceiptWindow : Window
{
    private readonly PaymentReceiptDto _receipt;

    public PaymentReceiptWindow(PaymentReceiptDto receipt)
    {
        InitializeComponent();
        _receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
        DataContext = new ReceiptViewModel(receipt);
    }

    private void SavePdfButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"{_receipt.ReceiptNumber}.pdf",
            Filter = "Document PDF (*.pdf)|*.pdf",
            Title = "Enregistrer le reçu en PDF"
        };

        if (dialog.ShowDialog(this) == true)
        {
            PaymentReceiptPdfBuilder.BuildReceiptPdf(_receipt, dialog.FileName);
            MessageBox.Show(
                this,
                "Le reçu PDF a été généré avec succès.",
                "PDF généré",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() == true)
        {
            dialog.PrintVisual(ReceiptVisual, $"Recu {_receipt.ReceiptNumber}");
        }
    }

    private void SaveTextButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"{_receipt.ReceiptNumber}.txt",
            Filter = "Fichier texte (*.txt)|*.txt",
            Title = "Exporter le reçu de paiement"
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, PaymentReceiptGenerator.FormatTextReceipt(_receipt));
            MessageBox.Show(
                this,
                "Le reçu a été exporté avec succès.",
                "Export réussi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class ReceiptViewModel
    {
        public ReceiptViewModel(PaymentReceiptDto receipt)
        {
            ReceiptNumber = receipt.ReceiptNumber;
            PaidAtLocal = receipt.PaidAt.ToLocalTime();
            CustomerName = receipt.CustomerName;
            OrderNumber = receipt.OrderNumber;
            Method = receipt.Method;
            MethodLabel = receipt.MethodLabel;
            CashierName = receipt.CashierName;
            Reference = receipt.Reference;
            OrderTotal = receipt.OrderTotal;
            AmountPaid = receipt.AmountPaid;
            TotalPaidOnOrder = receipt.TotalPaidOnOrder;
            RemainingBalance = receipt.RemainingBalance;
            IsFullyPaid = receipt.IsFullyPaid;
        }

        public string ReceiptNumber { get; }
        public DateTime PaidAtLocal { get; }
        public string CustomerName { get; }
        public string OrderNumber { get; }
        public PaymentMethod Method { get; }
        public string MethodLabel { get; }
        public string CashierName { get; }
        public string? Reference { get; }
        public bool HasReference => !string.IsNullOrWhiteSpace(Reference);
        public decimal OrderTotal { get; }
        public decimal AmountPaid { get; }
        public decimal TotalPaidOnOrder { get; }
        public decimal RemainingBalance { get; }
        public bool IsFullyPaid { get; }
    }
}
