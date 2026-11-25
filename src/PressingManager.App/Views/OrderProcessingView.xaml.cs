using System.Windows;
using System.Windows.Controls;
using PressingManager.App.Localization;
using PressingManager.App.ViewModels;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.Views;

/// <summary>
/// Commandes & Traitement : creation de commandes, consultation, paiement direct,
/// changement de statut et correction (verifie dans les services).
/// </summary>
public partial class OrderProcessingView : UserControl
{
    private readonly OrderProcessingViewModel _viewModel;
    private readonly ICustomerService _customers;
    private readonly IServiceCatalogService _services;
    private readonly IOrderService _orders;
    private readonly ICurrentUserContext _session;

    public OrderProcessingView(
        OrderProcessingViewModel viewModel,
        ICustomerService customers,
        IServiceCatalogService services,
        IOrderService orders,
        ICurrentUserContext session)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _session = session ?? throw new ArgumentNullException(nameof(session));

        DataContext = viewModel;
        _viewModel.PayOrderRequested += (_, request) => PayOrderRequested?.Invoke(this, request);

        // Confirmation avant une annulation (documentation, section 10.3).
        viewModel.Confirm = text =>
            MessageBox.Show(
                text,
                Loc.Instance["Proc_ConfirmTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public event EventHandler<PayOrderRequest>? PayOrderRequested;

    /// <summary>Recharge la file (appele a chaque ouverture de l'onglet ; sans effet sans le droit ViewOrders).</summary>
    public Task RefreshAsync() =>
        _viewModel.CanView ? _viewModel.LoadCommand.ExecuteAsync(null) : Task.CompletedTask;

    private async void BtnNewOrder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewOrderWindow(_customers, _services, _orders, _session)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.CreatedOrder is { } created)
        {
            await _viewModel.HandleOrderCreatedAsync(
                created,
                dialog.OpenPaymentAfterCreate,
                dialog.SelectedPaymentMethod);
        }
    }
}
