using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using PressingManager.App.Localization;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

/// <summary>
/// Onglet Rapports &amp; Statistiques. Il s'affiche dans la zone blanche du tableau de bord
/// (le menu lateral reste visible) ; <see cref="RefreshAsync"/> est appele a chaque ouverture de l'onglet.
/// </summary>
public partial class ReportsView : UserControl
{
    private readonly ReportsViewModel _viewModel;

    public ReportsView(ReportsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>Recharge les chiffres (sans effet si l'utilisateur n'a pas le droit ViewReports).</summary>
    public Task RefreshAsync()
    {
        _viewModel.RefreshPermissions();
        return _viewModel.CanViewReports ? _viewModel.RefreshDataAsync() : Task.CompletedTask;
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private async void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var path = AskFilePath(Loc.Instance["Stats_FilterCsv"], ".csv");
        if (path is not null)
        {
            await _viewModel.ExportAsync(path, asPdf: false);
        }
    }

    private async void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var path = AskFilePath(Loc.Instance["Stats_FilterPdf"], ".pdf");
        if (path is not null)
        {
            await _viewModel.ExportAsync(path, asPdf: true);
        }
    }

    private static string? AskFilePath(string filter, string extension)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension,
            FileName = $"rapport-{DateTime.Now:yyyyMMdd-HHmm}{extension}"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
