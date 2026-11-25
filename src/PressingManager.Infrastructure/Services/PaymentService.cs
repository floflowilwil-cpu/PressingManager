using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;
using PressingManager.Infrastructure.Data;
using PressingManager.Infrastructure.Repositories;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Service de gestion des paiements clients : encaissement, calcul du solde,
/// annulation et remboursement avec validation metier, controle des permissions et audit.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IOrderBillingInfoProvider _orderBillingProvider;
    private readonly ICurrentUserContext? _session;
    private readonly IAuditService? _audit;
    private readonly TimeProvider _time;

    [ActivatorUtilitiesConstructor]
    public PaymentService(
        IDbContextFactory<PressingDbContext> dbFactory,
        ICurrentUserContext session,
        IAuditService audit,
        TimeProvider time)
        : this(
            new EfPaymentRepository(dbFactory),
            new EfCashSessionRepository(dbFactory),
            new EfOrderBillingInfoProvider(dbFactory),
            session ?? throw new ArgumentNullException(nameof(session)),
            audit ?? throw new ArgumentNullException(nameof(audit)),
            time ?? throw new ArgumentNullException(nameof(time)))
    {
    }

    public PaymentService(
        IPaymentRepository paymentRepository,
        ICashSessionRepository cashSessionRepository,
        IOrderBillingInfoProvider orderBillingProvider,
        ICurrentUserContext? session = null,
        IAuditService? audit = null,
        TimeProvider? time = null)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _cashSessionRepository = cashSessionRepository ?? throw new ArgumentNullException(nameof(cashSessionRepository));
        _orderBillingProvider = orderBillingProvider ?? throw new ArgumentNullException(nameof(orderBillingProvider));
        _session = session;
        _audit = audit;
        _time = time ?? TimeProvider.System;
    }

    public async Task<PaymentDto> RegisterAsync(
        RegisterPaymentDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session?.Require(Permission.RegisterPayments);

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

        var userId = _session?.CurrentUser?.Id ?? request.ReceivedByUserId;
        var payment = new Payment
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            PaidAt = _time.GetUtcNow().UtcDateTime,
            Method = request.Method,
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            Status = PaymentStatus.Valid,
            ReceivedByUserId = userId,
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
        _session?.Require(Permission.RefundPayments);

        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Paiement #{request.PaymentId} introuvable.");

        PaymentRules.ValidateCancellationOrRefund(payment, request.Reason);

        var userId = _session?.CurrentUser?.Id ?? request.CancelledByUserId;
        payment.Status = targetStatus;
        payment.CancelReason = request.Reason.Trim();
        payment.CancelledByUserId = userId;
        payment.CancelledAt = _time.GetUtcNow().UtcDateTime;

        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        if (_audit is not null)
        {
            var action = targetStatus == PaymentStatus.Refunded
                ? AuditActions.PaymentRefunded
                : AuditActions.PaymentModified;

            var prefix = targetStatus == PaymentStatus.Refunded ? "Remboursement" : "Annulation";
            await _audit.LogAsync(
                action,
                nameof(Payment),
                payment.Id.ToString(),
                $"{prefix} ({payment.Amount}) : {payment.CancelReason}",
                userId,
                cancellationToken);
        }
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
