using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Repositories;

/// <summary>
/// Implementation en memoire de <see cref="IPaymentRepository"/>.
/// Utilisable pour les tests et le prototypage en attendant PressingDbContext (EF Core / SQLite).
/// </summary>
public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly List<Payment> _payments = new();
    private readonly Lock _sync = new();
    private int _nextId = 1;

    public Task<Payment?> GetByIdAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_payments.FirstOrDefault(p => p.Id == paymentId));
        }
    }

    public Task<IReadOnlyList<Payment>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            IReadOnlyList<Payment> result = _payments
                .Where(p => p.OrderId == orderId)
                .OrderBy(p => p.PaidAt)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<Payment>> GetByCashSessionIdAsync(int cashSessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            IReadOnlyList<Payment> result = _payments
                .Where(p => p.CashSessionId == cashSessionId)
                .OrderBy(p => p.PaidAt)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<bool> ExistsValidReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Task.FromResult(false);
        }

        var normalized = reference.Trim();
        lock (_sync)
        {
            var exists = _payments.Any(p =>
                p.Status == PaymentStatus.Valid
                && !string.IsNullOrWhiteSpace(p.Reference)
                && string.Equals(p.Reference.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(exists);
        }
    }

    public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(payment);

        lock (_sync)
        {
            if (payment.Id == 0)
            {
                payment.Id = _nextId++;
            }

            _payments.Add(payment);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(payment);

        lock (_sync)
        {
            var index = _payments.FindIndex(p => p.Id == payment.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"Paiement #{payment.Id} introuvable.");
            }

            _payments[index] = payment;
        }

        return Task.CompletedTask;
    }
}
