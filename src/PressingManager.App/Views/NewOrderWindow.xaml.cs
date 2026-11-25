using System.Windows;
using PressingManager.App.ViewModels;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.Views;

/// <summary>
/// Fenetre modale de creation d'une commande (selection client, ajout articles, calcul total et encaissement direct).
/// </summary>
public partial class NewOrderWindow : Window
{
    private readonly NewOrderViewModel _viewModel;

    public NewOrderWindow(
        ICustomerService customers,
        IServiceCatalogService services,
        IOrderService orders,
        ICurrentUserContext session)
    {
        InitializeComponent();

        _viewModel = new NewOrderViewModel(customers, services, orders, session);
        DataContext = _viewModel;

        _viewModel.OrderCreated += OnOrderCreated;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
        Closed += (_, _) => _viewModel.OrderCreated -= OnOrderCreated;
    }

    public OrderDetailsDto? CreatedOrder => _viewModel.CreatedOrder;

    public bool OpenPaymentAfterCreate => _viewModel.OpenPaymentAfterCreate;

    public PaymentMethod SelectedPaymentMethod => _viewModel.SelectedPaymentMethod;

    private void OnOrderCreated(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
