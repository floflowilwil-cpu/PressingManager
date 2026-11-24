using System.Windows;
using System.Windows.Controls;
using PressingManager.App.Localization;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

/// <summary>
/// Traitement des commandes. Lecture : ViewOrders ; changement de statut : UpdateOrderStatus ;
/// correction : CorrectOrderStatus (tout est aussi verifie dans OrderProcessingService).
/// </summary>
public partial class OrderProcessingView : UserControl
{
    private readonly OrderProcessingViewModel _viewModel;

    public OrderProcessingView(OrderProcessingViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        // Confirmation avant une annulation (documentation, section 10.3).
        viewModel.Confirm = text =>
            MessageBox.Show(
                text,
                Loc.Instance["Proc_ConfirmTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    /// <summary>Recharge la file (appele a chaque ouverture de l'onglet ; sans effet sans le droit ViewOrders).</summary>
    public Task RefreshAsync() =>
        _viewModel.CanView ? _viewModel.LoadCommand.ExecuteAsync(null) : Task.CompletedTask;
}
