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
/// Service de gestion des sessions de caisse (ouverture unique, consultation et cloture avec calcul d'ecart).
/// </summary>
public sealed class CashService : ICashService
{
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICurrentUserContext? _session;
    private readonly IAuditService? _audit;
    private readonly TimeProvider _time;

    [ActivatorUtilitiesConstructor]
    public CashService(
        IDbContextFactory<PressingDbContext> dbFactory,
        ICurrentUserContext session,
        IAuditService audit,
        TimeProvider time)
        : this(
            new EfCashSessionRepository(dbFactory),
            new EfPaymentRepository(dbFactory),
            new EfExpenseRepository(dbFactory),
            session ?? throw new ArgumentNullException(nameof(session)),
            audit ?? throw new ArgumentNullException(nameof(audit)),
            time ?? throw new ArgumentNullException(nameof(time)))
    {
    }

    public CashService(
        ICashSessionRepository cashSessionRepository,
        IPaymentRepository paymentRepository,
        IExpenseRepository expenseRepository,
        ICurrentUserContext? session = null,
        IAuditService? audit = null,
        TimeProvider? time = null)
    {
        _cashSessionRepository = cashSessionRepository ?? throw new ArgumentNullException(nameof(cashSessionRepository));
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _expenseRepository = expenseRepository ?? throw new ArgumentNullException(nameof(expenseRepository));
        _session = session;
        _audit = audit;
        _time = time ?? TimeProvider.System;
    }

    public async Task<CashSessionDto> OpenAsync(
        OpenCashSessionDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session?.Require(Permission.RegisterPayments);

        if (request.OpeningAmount < 0m)
        {
            throw new InvalidOperationException("Le fonds d'ouverture de caisse ne peut pas etre negatif.");
        }

        var existingOpen = await _cashSessionRepository.GetOpenSessionAsync(cancellationToken);
        if (existingOpen is not null)
        {
            throw new InvalidOperationException("Une session de caisse est deja ouverte.");
        }

        var userId = _session?.CurrentUser?.Id ?? request.OpenedByUserId;
        var cashSession = new CashSession
        {
            OpenedAt = _time.GetUtcNow().UtcDateTime,
            OpeningAmount = request.OpeningAmount,
            OpenedByUserId = userId,
            Status = CashSessionStatus.Open
        };

        await _cashSessionRepository.AddAsync(cashSession, cancellationToken);
        return ToDto(cashSession);
    }

    public async Task<CashSessionDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var session = await _cashSessionRepository.GetOpenSessionAsync(cancellationToken);
        return session is null ? null : ToDto(session);
    }

    public async Task<CashSessionDto> CloseAsync(
        CloseCashSessionDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session?.Require(Permission.CloseCashSession);

        if (request.CountedAmount < 0m)
        {
            throw new InvalidOperationException("Le montant compte a la cloture ne peut pas etre negatif.");
        }

        var cashSession = await _cashSessionRepository.GetByIdAsync(request.CashSessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session de caisse #{request.CashSessionId} introuvable.");

        if (cashSession.Status != CashSessionStatus.Open)
        {
            throw new InvalidOperationException("Cette session de caisse est deja cloturee.");
        }

        var payments = await _paymentRepository.GetByCashSessionIdAsync(cashSession.Id, cancellationToken);
        var expenses = await _expenseRepository.GetByCashSessionIdAsync(cashSession.Id, cancellationToken);

        var expectedAmount = CashCalculator.GetExpectedAmount(cashSession.OpeningAmount, payments, expenses);
        var discrepancy = CashCalculator.GetDiscrepancy(request.CountedAmount, expectedAmount);

        var userId = _session?.CurrentUser?.Id ?? request.ClosedByUserId;
        cashSession.ClosedAt = _time.GetUtcNow().UtcDateTime;
        cashSession.CountedAmount = request.CountedAmount;
        cashSession.ExpectedAmount = expectedAmount;
        cashSession.Discrepancy = discrepancy;
        cashSession.ClosedByUserId = userId;
        cashSession.CloseComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        cashSession.Status = CashSessionStatus.Closed;

        await _cashSessionRepository.UpdateAsync(cashSession, cancellationToken);

        if (_audit is not null)
        {
            await _audit.LogAsync(
                AuditActions.CashSessionClosed,
                nameof(CashSession),
                cashSession.Id.ToString(),
                $"Attendu={expectedAmount}, Compte={request.CountedAmount}, Ecart={discrepancy}",
                userId,
                cancellationToken);
        }

        return ToDto(cashSession);
    }

    private static CashSessionDto ToDto(CashSession session) => new(
        session.Id,
        session.OpenedAt,
        session.OpeningAmount,
        session.OpenedByUserId,
        session.ClosedAt,
        session.CountedAmount,
        session.ExpectedAmount,
        session.Discrepancy,
        session.ClosedByUserId,
        session.CloseComment,
        session.Status);
}
