using System.Windows.Controls;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

/// <summary>Services et tarifs. Lecture ouverte ; ecriture reservee a ManageServices (verifiee aussi dans le service).</summary>
public partial class ServiceCatalogView : UserControl
{
    private readonly ServiceCatalogViewModel _viewModel;

    public ServiceCatalogView(ServiceCatalogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>Recharge le catalogue (appele a chaque ouverture de l'onglet).</summary>
    public Task RefreshAsync() => _viewModel.LoadCommand.ExecuteAsync(null);
}
