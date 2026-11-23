using System.Windows;
using System.Windows.Controls;
using PressingManager.App.ViewModels;
using PressingManager.Core.DTOs;

namespace PressingManager.App.Views;

/// <summary>
/// Ecran WPF de gestion des paiements, validation visuelle, generation de recus, caisse et depenses.
/// </summary>
public partial class PaymentManagementView : UserControl
{
    public PaymentManagementView(PaymentManagementViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        viewModel.ReceiptReady += OnReceiptReady;

        Loaded += async (_, _) => await viewModel.LoadCommand.ExecuteAsync(null);
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
