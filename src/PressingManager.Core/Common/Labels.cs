using PressingManager.Core.Enums;

namespace PressingManager.Core.Common;

/// <summary>Libelles francais des roles, pour l'affichage.</summary>
public static class UserRoleLabels
{
    public static string ToFrench(UserRole role) => role switch
    {
        UserRole.Administrator => "Administrateur",
        UserRole.FrontDesk => "Agent d'accueil",
        UserRole.Cashier => "Caissier",
        UserRole.Processor => "Agent de traitement",
        UserRole.StockManager => "Responsable stock",
        _ => role.ToString()
    };
}

/// <summary>Libelles francais des statuts de commande (documentation, section 5.2).</summary>
public static class OrderStatusLabels
{
    public static string ToFrench(OrderStatus status) => status switch
    {
        OrderStatus.Received => "Reçu",
        OrderStatus.AwaitingProcessing => "En attente de traitement",
        OrderStatus.Cleaning => "En nettoyage",
        OrderStatus.Ironing => "En repassage",
        OrderStatus.ReadyForPickup => "Prêt à récupérer",
        OrderStatus.Delivered => "Livré",
        OrderStatus.Cancelled => "Annulé",
        _ => status.ToString()
    };
}
