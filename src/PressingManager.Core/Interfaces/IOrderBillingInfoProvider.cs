using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>
/// Contrat d'integration avec le module Commandes (Personne 2) pour consulter
/// le montant total et l'etat d'annulation d'une commande sans couplage fort.
/// </summary>
public interface IOrderBillingInfoProvider
{
    Task<OrderBillingInfoDto?> GetOrderBillingInfoAsync(
        int orderId,
        CancellationToken cancellationToken = default);
}
