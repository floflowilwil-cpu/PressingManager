using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>
/// Gestion des paiements clients : encaissement, solde restant, annulation et remboursement.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Enregistre un nouveau paiement apres validation des regles metier
    /// (montant positif, pas de trop-percu, reference Mobile Money unique, caisse ouverte si Cash).
    /// </summary>
    Task<PaymentDto> RegisterAsync(
        RegisterPaymentDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Liste des paiements associes a une commande.</summary>
    Task<IReadOnlyList<PaymentDto>> GetByOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>Solde restant a payer pour une commande (total - paiements valides).</summary>
    Task<decimal> GetBalanceAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>Annule un paiement valide avec motif obligatoire et tracabilite.</summary>
    Task CancelAsync(
        CancelPaymentDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Rembourse un paiement valide avec motif obligatoire et tracabilite.</summary>
    Task RefundAsync(
        CancelPaymentDto request,
        CancellationToken cancellationToken = default);
}
