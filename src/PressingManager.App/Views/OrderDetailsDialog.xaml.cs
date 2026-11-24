using System.Windows;
using PressingManager.App.ViewModels;

namespace PressingManager.App.Views;

/// <summary>Detail d'une commande : articles, remise, frais et total.</summary>
public partial class OrderDetailsDialog : Window
{
    public OrderDetailsDialog(OrderRow row)
    {
        InitializeComponent();
        DataContext = row ?? throw new ArgumentNullException(nameof(row));
    }
}
