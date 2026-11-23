using PressingManager.Core.Entities;

namespace PressingManager.Core.Interfaces;

/// <summary>Acces aux donnees des paiements (implementable en memoire ou via EF Core).</summary>
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(int paymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Payment>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Payment>> GetByCashSessionIdAsync(int cashSessionId, CancellationToken cancellationToken = default);

    Task<bool> ExistsValidReferenceAsync(string reference, CancellationToken cancellationToken = default);

    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
}
