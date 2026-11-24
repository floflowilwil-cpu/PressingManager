using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PressingManager.App.Localization;
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
    private readonly ReceiptViewModel _viewModel;

    public PaymentReceiptWindow(PaymentReceiptDto receipt)
    {
        InitializeComponent();
        _receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
        _viewModel = new ReceiptViewModel(receipt);
        DataContext = _viewModel;

        Loc.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => Loc.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => _viewModel.RefreshTranslations();

    private void SavePdfButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"{_receipt.ReceiptNumber}.pdf",
            Filter = Loc.Instance["Receipt_SavePdfFilter"],
            Title = Loc.Instance["Receipt_SavePdfTitle"]
        };

        if (dialog.ShowDialog(this) == true)
        {
            PaymentReceiptPdfBuilder.BuildReceiptPdf(_receipt, dialog.FileName);
            MessageBox.Show(
                this,
                Loc.Instance["Receipt_PdfSuccessMsg"],
                Loc.Instance["Receipt_PdfSuccessTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() == true)
        {
            dialog.PrintVisual(ReceiptVisual, Loc.Instance.Format("Receipt_PrintJobName", _receipt.ReceiptNumber));
        }
    }

    private void SaveTextButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"{_receipt.ReceiptNumber}.txt",
            Filter = Loc.Instance["Receipt_SaveTxtFilter"],
            Title = Loc.Instance["Receipt_SaveTxtTitle"]
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, PaymentReceiptGenerator.FormatTextReceipt(_receipt));
            MessageBox.Show(
                this,
                Loc.Instance["Receipt_TxtSuccessMsg"],
                Loc.Instance["Receipt_TxtSuccessTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class ReceiptViewModel : INotifyPropertyChanged
    {
        public ReceiptViewModel(PaymentReceiptDto receipt)
        {
            ReceiptNumber = receipt.ReceiptNumber;
            PaidAtLocal = receipt.PaidAt.ToLocalTime();
            CustomerName = receipt.CustomerName;
            OrderNumber = receipt.OrderNumber;
            Method = receipt.Method;
            CashierName = receipt.CashierName;
            Reference = receipt.Reference;
            OrderTotal = receipt.OrderTotal;
            AmountPaid = receipt.AmountPaid;
            TotalPaidOnOrder = receipt.TotalPaidOnOrder;
            RemainingBalance = receipt.RemainingBalance;
            IsFullyPaid = receipt.IsFullyPaid;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ReceiptNumber { get; }
        public DateTime PaidAtLocal { get; }
        public string CustomerName { get; }
        public string OrderNumber { get; }
        public PaymentMethod Method { get; }
        public string CashierName { get; }
        public string? Reference { get; }
        public bool HasReference => !string.IsNullOrWhiteSpace(Reference);
        public decimal OrderTotal { get; }
        public decimal AmountPaid { get; }
        public decimal TotalPaidOnOrder { get; }
        public decimal RemainingBalance { get; }
        public bool IsFullyPaid { get; }

        public string MethodLabel => Loc.Instance.PaymentMethod(Method);

        public string StatusBadgeText => Loc.Instance[IsFullyPaid ? "Receipt_StatusPaid" : "Receipt_StatusPartial"];

        public string RecordedOnText => Loc.Instance.Format(
            "Receipt_RecordedOn",
            PaidAtLocal.ToString("dd/MM/yyyy HH:mm", Loc.Instance.Culture));

        public void RefreshTranslations()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MethodLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusBadgeText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecordedOnText)));
        }
    }
}
