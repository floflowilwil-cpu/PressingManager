using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Service de gestion des paiements clients : encaissement, calcul du solde,
/// annulation et remboursement avec validation stricte des regles metier.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IOrderBillingInfoProvider _orderBillingProvider;

    public PaymentService(
        IPaymentRepository paymentRepository,
        ICashSessionRepository cashSessionRepository,
        IOrderBillingInfoProvider orderBillingProvider)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _cashSessionRepository = cashSessionRepository ?? throw new ArgumentNullException(nameof(cashSessionRepository));
        _orderBillingProvider = orderBillingProvider ?? throw new ArgumentNullException(nameof(orderBillingProvider));
    }

    public async Task<PaymentDto> RegisterAsync(
        RegisterPaymentDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await _orderBillingProvider.GetOrderBillingInfoAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Commande #{request.OrderId} introuvable.");

        if (order.IsCancelled)
        {
            throw new InvalidOperationException(
                $"Impossible d'encaisser la commande #{request.OrderId} : elle est annulee.");
        }

        var existingPayments = await _paymentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        var openSession = PaymentRules.RequiresCashSession(request.Method)
            ? await _cashSessionRepository.GetOpenSessionAsync(cancellationToken)
            : null;

        PaymentRules.ValidatePayment(
            order.TotalAmount,
            existingPayments,
            request.Amount,
            request.Method,
            request.Reference,
            openSession is not null);

        if (PaymentRules.RequiresReference(request.Method)
            && await _paymentRepository.ExistsValidReferenceAsync(request.Reference!, cancellationToken))
        {
            throw new InvalidOperationException(
                $"La reference de transaction '{request.Reference!.Trim()}' existe deja.");
        }

        var payment = new Payment
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            PaidAt = DateTime.UtcNow,
            Method = request.Method,
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            Status = PaymentStatus.Valid,
            ReceivedByUserId = request.ReceivedByUserId,
            CashSessionId = openSession?.Id
        };

        await _paymentRepository.AddAsync(payment, cancellationToken);
        return ToDto(payment);
    }

    public async Task<IReadOnlyList<PaymentDto>> GetByOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var payments = await _paymentRepository.GetByOrderIdAsync(orderId, cancellationToken);
        return payments
            .OrderBy(p => p.PaidAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task<decimal> GetBalanceAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderBillingProvider.GetOrderBillingInfoAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Commande #{orderId} introuvable.");

        var payments = await _paymentRepository.GetByOrderIdAsync(orderId, cancellationToken);
        return PaymentRules.GetRemainingBalance(order.TotalAmount, payments);
    }

    public Task CancelAsync(CancelPaymentDto request, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(request, PaymentStatus.Cancelled, cancellationToken);

    public Task RefundAsync(CancelPaymentDto request, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(request, PaymentStatus.Refunded, cancellationToken);

    private async Task ChangeStatusAsync(
        CancelPaymentDto request,
        PaymentStatus targetStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Paiement #{request.PaymentId} introuvable.");

        PaymentRules.ValidateCancellationOrRefund(payment, request.Reason);

        payment.Status = targetStatus;
        payment.CancelReason = request.Reason.Trim();
        payment.CancelledByUserId = request.CancelledByUserId;
        payment.CancelledAt = DateTime.UtcNow;

        await _paymentRepository.UpdateAsync(payment, cancellationToken);
    }

    private static PaymentDto ToDto(Payment payment) => new(
        payment.Id,
        payment.OrderId,
        payment.Amount,
        payment.PaidAt,
        payment.Method,
        payment.Reference,
        payment.Status,
        payment.ReceivedByUserId,
        payment.CashSessionId,
        payment.CancelReason,
        payment.CancelledByUserId,
        payment.CancelledAt);
}
