using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs
{
    // ---------- CLIENTS ----------

    public record CustomerDto(
        int Id,
        string CustomerNumber,
        string FullName,
        string Phone,
        string? Address,
        string? Notes,
        bool IsActive,
        DateTime CreatedAt);

    public record CreateCustomerDto(
        string FullName,
        string Phone,
        string? Address = null,
        string? Notes = null);

    public record UpdateCustomerDto(
        int Id,
        string FullName,
        string Phone,
        string? Address = null,
        string? Notes = null);

    // ---------- COMMANDES ----------

    // Une ligne à saisir lors de la création d'une commande
    public record CreateOrderItemDto(
        int ServiceId,
        string ItemType,
        int Quantity,
        decimal UnitPrice,
        string? Observations = null);

    public record CreateOrderDto(
        int CustomerId,
        List<CreateOrderItemDto> Items,
        DateTime? ExpectedDate = null,
        decimal Discount = 0m,
        decimal Fees = 0m,
        string? Notes = null);

    // Modification d'une commande existante : les lignes envoyées REMPLACENT les anciennes.
    // Le client ne change pas.
    public record UpdateOrderDto(
        int OrderId,
        List<CreateOrderItemDto> Items,
        DateTime? ExpectedDate = null,
        decimal Discount = 0m,
        decimal Fees = 0m,
        string? Notes = null);

    // Annulation d'une commande (le motif est obligatoire)
    public record CancelOrderDto(
        int OrderId,
        string Reason);

    // Une ligne telle qu'elle est renvoyée
    public record OrderItemDto(
        int Id,
        int ServiceId,
        string ItemType,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal,
        string? Observations);

    // Détail complet d'une commande
    public record OrderDetailsDto(
        int Id,
        string OrderNumber,
        int CustomerId,
        string CustomerName,
        DateTime CreatedAt,
        DateTime? ExpectedDate,
        OrderStatus Status,
        decimal Subtotal,
        decimal Discount,
        decimal Fees,
        decimal Total,
        string? Notes,
        List<OrderItemDto> Items);

    // Critères de recherche de commandes (tous facultatifs).
    // SearchText : numéro de commande, nom ou téléphone du client.
    // FromDate / ToDate : jours inclus, d'après la date de création (heure locale).
    public record OrderSearchDto(
        string? SearchText = null,
        OrderStatus? Status = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null);

    // ---------- VUE D'ENSEMBLE (cartes et tableau de l'onglet Commandes) ----------

    // Compteurs et montants sur TOUTES les commandes du filtre.
    // En attente = Reçu + En attente de traitement ; En cours = Nettoyage + Repassage ;
    // Terminées = Prêt à récupérer + Livré. Les montants ignorent les commandes annulées.
    public record OrderStatsDto(
        int TotalOrders,
        int PendingOrders,
        int InProgressOrders,
        int CompletedOrders,
        int CancelledOrders,
        decimal BilledAmount,
        decimal CollectedAmount,
        decimal RemainingAmount);

    // Une ligne du tableau : la commande avec ce qui a été payé et ce qui reste à payer.
    public record OrderOverviewRowDto(
        int OrderId,
        string OrderNumber,
        int CustomerId,
        string CustomerName,
        string CustomerPhone,
        DateTime CreatedAt,
        DateTime? ExpectedDate,
        OrderStatus Status,
        int ItemCount,
        decimal Total,
        decimal AmountPaid,
        decimal Balance);

    public record OrderOverviewDto(
        OrderStatsDto Stats,
        IReadOnlyList<OrderOverviewRowDto> Rows);
}

namespace PressingManager.Core.Interfaces
{
    using PressingManager.Core.DTOs;

    public interface ICustomerService
    {
        // Contrats de l'Annexe B de la documentation
        Task<IReadOnlyList<CustomerDto>> SearchAsync(
            string searchText,
            CancellationToken cancellationToken = default);

        Task<CustomerDto> CreateAsync(
            CreateCustomerDto request,
            CancellationToken cancellationToken = default);

        // Ajouts de la personne 2
        Task<CustomerDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<CustomerDto> UpdateAsync(
            UpdateCustomerDto request,
            CancellationToken cancellationToken = default);

        Task DeactivateAsync(
            int id,
            CancellationToken cancellationToken = default);

        // Clients désactivés (pour proposer une réactivation)
        Task<IReadOnlyList<CustomerDto>> SearchInactiveAsync(
            string searchText,
            CancellationToken cancellationToken = default);

        Task<CustomerDto> ReactivateAsync(
            int id,
            CancellationToken cancellationToken = default);
    }

    public interface IOrderService
    {
        // Contrats de l'Annexe B de la documentation
        Task<OrderDetailsDto> CreateAsync(
            CreateOrderDto request,
            CancellationToken cancellationToken = default);

        Task<OrderDetailsDto?> GetByNumberAsync(
            string orderNumber,
            CancellationToken cancellationToken = default);

        // Ajouts de la personne 2
        Task<OrderDetailsDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OrderDetailsDto>> GetByCustomerAsync(
            int customerId,
            CancellationToken cancellationToken = default);

        // Recherche filtrée (200 résultats maximum, les plus récents d'abord)
        Task<IReadOnlyList<OrderDetailsDto>> SearchAsync(
            OrderSearchDto filter,
            CancellationToken cancellationToken = default);

        // Cartes statistiques + tableau (avec payé et reste à payer) pour un même filtre
        Task<OrderOverviewDto> GetOverviewAsync(
            OrderSearchDto filter,
            CancellationToken cancellationToken = default);

        // Modification : seulement tant que la commande n'est pas en traitement (voir OrderEditRules)
        Task<OrderDetailsDto> UpdateAsync(
            UpdateOrderDto request,
            CancellationToken cancellationToken = default);

        // Annulation avec motif : refusée si la commande a encore des paiements valides
        Task<OrderDetailsDto> CancelAsync(
            CancelOrderDto request,
            CancellationToken cancellationToken = default);
    }
}

namespace PressingManager.Core.Common
{
    /// <summary>
    /// Règles simples que l'écran Commandes utilise pour activer ou griser les boutons.
    /// Les services appliquent les mêmes règles : l'écran n'est qu'un confort visuel.
    /// </summary>
    public static class OrderEditRules
    {
        /// <summary>Modifiable tant que le traitement n'a pas commencé.</summary>
        public static bool CanEdit(OrderStatus status) =>
            status is OrderStatus.Received or OrderStatus.AwaitingProcessing;

        /// <summary>Annulable selon les transitions de statut (pas une commande livrée ou déjà annulée).</summary>
        public static bool CanCancel(OrderStatus status) =>
            OrderStatusRules.GetNextStatuses(status).Contains(OrderStatus.Cancelled);
    }
}