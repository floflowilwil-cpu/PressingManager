using System.Windows;
using System.Windows.Controls;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

/// <summary>Onglet « Commandes » (maquette : tableau avec badges de statut, recherche, nouvelle commande).</summary>
public partial class OrdersView : UserControl
{
    private readonly OrdersViewModel _viewModel;
    private readonly Func<NewOrderViewModel> _newOrderFactory;

    public OrdersView(OrdersViewModel viewModel, Func<NewOrderViewModel> newOrderFactory)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _newOrderFactory = newOrderFactory ?? throw new ArgumentNullException(nameof(newOrderFactory));
        DataContext = viewModel;
    }

    /// <summary>Appele par le menu a chaque ouverture de l'onglet.</summary>
    public Task RefreshAsync() => _viewModel.LoadCommand.ExecuteAsync(null);

    private async void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewOrderDialog(_newOrderFactory())
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.CreatedOrder is { } order)
        {
            await _viewModel.OnOrderCreatedAsync(order);
        }
    }

    private void BtnView_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is OrderRow row)
        {
            OpenDetails(row);
        }
    }

    private void Grid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && !IsInsideButton(source)
            && OrdersGrid.SelectedItem is OrderRow row)
        {
            OpenDetails(row);
        }
    }

    private void OpenDetails(OrderRow row)
    {
        var dialog = new OrderDetailsDialog(row)
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
    }

    private static bool IsInsideButton(DependencyObject start)
    {
        var current = start;
        while (current is not null)
        {
            if (current is Button)
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
