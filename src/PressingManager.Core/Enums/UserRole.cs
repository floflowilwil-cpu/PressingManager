namespace PressingManager.Core.Enums;

/// <summary>
/// Roles des utilisateurs (documentation, section 3).
/// Valeurs explicites car stockees en base : ne jamais renumeroter, ajouter a la fin.
/// </summary>
public enum UserRole
{
    /// <summary>Administrateur / gerant : acces complet.</summary>
    Administrator = 0,

    /// <summary>Agent d'accueil : clients et commandes.</summary>
    FrontDesk = 1,

    /// <summary>Caissier : paiements et caisse.</summary>
    Cashier = 2,

    /// <summary>Agent de traitement : avancement du linge.</summary>
    Processor = 3,

    /// <summary>Responsable stock : produits et mouvements.</summary>
    StockManager = 4
}
