using System.Windows;
using PressingManager.App.ViewModels;
using PressingManager.Core.DTOs;

namespace PressingManager.App.Views;

/// <summary>Creation d'une commande multi-articles (documentation, section 4.3 et parcours 5.1).</summary>
public partial class NewOrderDialog : Window
{
    private readonly NewOrderViewModel _viewModel;

    public NewOrderDialog(NewOrderViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    /// <summary>La commande creee, renseignee quand la fenetre se ferme avec succes.</summary>
    public OrderDetailsDto? CreatedOrder { get; private set; }

    private async void BtnCreate_Click(object sender, RoutedEventArgs e)
    {
        var order = await _viewModel.SubmitAsync();
        if (order is not null)
        {
            CreatedOrder = order;
            DialogResult = true;
        }
    }
}
