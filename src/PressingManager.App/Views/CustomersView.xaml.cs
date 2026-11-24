using System.Windows;
using System.Windows.Controls;
using PressingManager.App.ViewModels;
using PressingManager.Core.DTOs;

namespace PressingManager.App.Views;

/// <summary>Onglet « Clients » (maquette : tableau, recherche, actions voir / modifier / desactiver).</summary>
public partial class CustomersView : UserControl
{
    private readonly CustomersViewModel _viewModel;

    public CustomersView(CustomersViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
    }

    /// <summary>Appele par le menu a chaque ouverture de l'onglet.</summary>
    public Task RefreshAsync() => _viewModel.LoadCommand.ExecuteAsync(null);

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomerEditDialog(
            existing: null,
            save: (name, phone, address, notes) => _viewModel.CreateAsync(name, phone, address, notes))
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerOf(sender) is { } customer)
        {
            OpenEdit(customer);
        }
    }

    private async void BtnView_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerOf(sender) is { } customer)
        {
            await OpenDetailsAsync(customer);
        }
    }

    private async void Grid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Double-clic sur une ligne = ouvrir la fiche (les clics sur les boutons d'action sont ignores).
        if (e.OriginalSource is DependencyObject source
            && FindAncestor<Button>(source) is null
            && CustomersGrid.SelectedItem is CustomerDto customer)
        {
            await OpenDetailsAsync(customer);
        }
    }

    private async void BtnDeactivate_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerOf(sender) is not { } customer)
        {
            return;
        }

        var answer = MessageBox.Show(
            Window.GetWindow(this),
            $"Désactiver le client « {customer.FullName} » ?\n\nIl n'apparaîtra plus dans la liste ni pour les nouvelles commandes, mais son historique est conservé.",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await _viewModel.DeactivateAsync(customer);
        }
    }

    private void OpenEdit(CustomerDto customer)
    {
        var dialog = new CustomerEditDialog(
            existing: customer,
            save: (name, phone, address, notes) => _viewModel.UpdateAsync(customer.Id, name, phone, address, notes))
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
    }

    private async Task OpenDetailsAsync(CustomerDto customer)
    {
        var model = await _viewModel.GetDetailsAsync(customer);

        var dialog = new CustomerDetailsDialog(model)
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
    }

    private static CustomerDto? CustomerOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as CustomerDto;

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
