using System.Windows;

namespace PressingManager.App.Views;

/// <summary>
/// Fenetre d'accueil provisoire de l'onglet Statistiques, en attendant la navigation de la Personne 1.
/// Quand la zone de contenu du menu existera, il suffira d'y placer directement <see cref="ReportsView"/>.
/// </summary>
public partial class ReportsWindow : Window
{
    public ReportsWindow(ReportsView view)
    {
        InitializeComponent();
        Content = view;
    }
}
