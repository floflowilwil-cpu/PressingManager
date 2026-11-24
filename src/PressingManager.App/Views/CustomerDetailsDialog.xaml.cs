using System.Windows;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

/// <summary>Fiche d'un client avec l'historique de ses commandes (documentation, section 4.2).</summary>
public partial class CustomerDetailsDialog : Window
{
    public CustomerDetailsDialog(CustomerDetailsModel model)
    {
        InitializeComponent();
        DataContext = model ?? throw new ArgumentNullException(nameof(model));
    }
}
