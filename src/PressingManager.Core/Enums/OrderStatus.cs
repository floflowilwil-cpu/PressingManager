namespace PressingManager.Core.Enums;

/// <summary>
/// Statuts d'une commande (documentation, section 5.2).
/// Les valeurs numeriques sont explicites car elles seront stockees en base :
/// ne jamais les renumeroter, ajouter uniquement a la fin.
/// </summary>
public enum OrderStatus
{
    /// <summary>La commande vient d'etre enregistree.</summary>
    Received = 0,

    /// <summary>Les articles attendent leur prise en charge.</summary>
    AwaitingProcessing = 1,

    /// <summary>Le nettoyage est en cours.</summary>
    Cleaning = 2,

    /// <summary>Le repassage ou la finition est en cours.</summary>
    Ironing = 3,

    /// <summary>La commande est terminee et disponible.</summary>
    ReadyForPickup = 4,

    /// <summary>Les articles ont ete remis au client.</summary>
    Delivered = 5,

    /// <summary>La commande a ete annulee.</summary>
    Cancelled = 6
}
