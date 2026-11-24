using System.Windows.Controls;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

/// <summary>Stocks. Lecture : ViewStock ; entrees, sorties et produits : ManageStock (verifie aussi dans le service).</summary>
public partial class StockView : UserControl
{
    private readonly StockViewModel _viewModel;

    public StockView(StockViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>Recharge les produits (appele a chaque ouverture de l'onglet ; sans effet sans le droit ViewStock).</summary>
    public Task RefreshAsync() =>
        _viewModel.CanView ? _viewModel.LoadCommand.ExecuteAsync(null) : Task.CompletedTask;
}
