using PressingManager.Core.Enums;

namespace PressingManager.Core.Common;

/// <summary>
/// Regles de transition entre statuts de commande (documentation, section 5.2).
/// Classe pure : aucune dependance a la base de donnees, donc facile a tester.
/// </summary>
public static class OrderStatusRules
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Allowed =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Received] = new[] { OrderStatus.AwaitingProcessing, OrderStatus.Cancelled },
            [OrderStatus.AwaitingProcessing] = new[] { OrderStatus.Cleaning, OrderStatus.Cancelled },
            [OrderStatus.Cleaning] = new[] { OrderStatus.Ironing, OrderStatus.ReadyForPickup, OrderStatus.Cancelled },
            [OrderStatus.Ironing] = new[] { OrderStatus.ReadyForPickup, OrderStatus.Cancelled },
            [OrderStatus.ReadyForPickup] = new[] { OrderStatus.Delivered, OrderStatus.Cancelled },
            [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
            [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
        };

    /// <summary>Statuts atteignables depuis <paramref name="from"/> dans le flux normal.</summary>
    public static IReadOnlyCollection<OrderStatus> GetNextStatuses(OrderStatus from) => Allowed[from];

    /// <summary>
    /// Indique si la transition est permise.
    /// Avec <paramref name="isAuthorizedCorrection"/> = true, un retour en arriere est
    /// accepte (ex. Delivered vers ReadyForPickup). Une commande annulee reste definitive,
    /// et on ne peut pas "corriger" vers Cancelled : l'annulation suit le flux normal.
    /// Les corrections doivent etre tracees dans OrderStatusHistory (IsCorrection).
    /// </summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to, bool isAuthorizedCorrection = false)
    {
        if (Allowed[from].Contains(to))
        {
            return true;
        }

        return isAuthorizedCorrection
               && from != OrderStatus.Cancelled
               && to != OrderStatus.Cancelled
               && (int)to < (int)from;
    }
}
