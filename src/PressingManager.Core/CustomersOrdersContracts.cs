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

        // Ajout de la personne 2
        Task<IReadOnlyList<OrderDetailsDto>> GetByCustomerAsync(
            int customerId,
            CancellationToken cancellationToken = default);

        // Ajout (intégration UI) : liste des commandes pour l'onglet « Commandes ».
        // Recherche par numéro de commande ou nom du client ; vide = les plus récentes.
        Task<IReadOnlyList<OrderDetailsDto>> SearchAsync(
            string searchText,
            CancellationToken cancellationToken = default);
    }
}