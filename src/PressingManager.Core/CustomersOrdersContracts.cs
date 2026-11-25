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
        DateTime CreatedAt,
        string? Email = null);

    public record CreateCustomerDto(
        string FullName,
        string Phone,
        string? Address = null,
        string? Notes = null,
        string? Email = null);

    public record UpdateCustomerDto(
        int Id,
        string FullName,
        string Phone,
        string? Address = null,
        string? Notes = null,
        string? Email = null);

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
        List<OrderItemDto> Items,
        string? CustomerPhone = null,
        string? CustomerEmail = null);

    // Critères de recherche de commandes (tous facultatifs).
    // SearchText : numéro de commande, nom ou téléphone du client.
    // FromDate / ToDate : jours inclus, d'après la date de création (heure locale).
    public record OrderSearchDto(
        string? SearchText = null,
        OrderStatus? Status = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null);
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
        Task<IReadOnlyList<OrderDetailsDto>> GetByCustomerAsync(
            int customerId,
            CancellationToken cancellationToken = default);

        // Recherche filtrée (200 résultats maximum, les plus récents d'abord)
        Task<IReadOnlyList<OrderDetailsDto>> SearchAsync(
            OrderSearchDto filter,
            CancellationToken cancellationToken = default);
    }
}