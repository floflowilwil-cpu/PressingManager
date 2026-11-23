using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Service de gestion des sessions de caisse (ouverture unique, consultation et cloture avec calcul d'ecart).
/// </summary>
public sealed class CashService : ICashService
{
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IExpenseRepository _expenseRepository;

    public CashService(
        ICashSessionRepository cashSessionRepository,
        IPaymentRepository paymentRepository,
        IExpenseRepository expenseRepository)
    {
        _cashSessionRepository = cashSessionRepository ?? throw new ArgumentNullException(nameof(cashSessionRepository));
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _expenseRepository = expenseRepository ?? throw new ArgumentNullException(nameof(expenseRepository));
    }

    public async Task<CashSessionDto> OpenAsync(
        OpenCashSessionDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OpeningAmount < 0m)
        {
            throw new InvalidOperationException("Le fonds d'ouverture de caisse ne peut pas etre negatif.");
        }

        var existingOpen = await _cashSessionRepository.GetOpenSessionAsync(cancellationToken);
        if (existingOpen is not null)
        {
            throw new InvalidOperationException("Une session de caisse est deja ouverte.");
        }

        var session = new CashSession
        {
            OpenedAt = DateTime.UtcNow,
            OpeningAmount = request.OpeningAmount,
            OpenedByUserId = request.OpenedByUserId,
            Status = CashSessionStatus.Open
        };

        await _cashSessionRepository.AddAsync(session, cancellationToken);
        return ToDto(session);
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

        if (request.CountedAmount < 0m)
        {
            throw new InvalidOperationException("Le montant compte a la cloture ne peut pas etre negatif.");
        }

        var session = await _cashSessionRepository.GetByIdAsync(request.CashSessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session de caisse #{request.CashSessionId} introuvable.");

        if (session.Status != CashSessionStatus.Open)
        {
            throw new InvalidOperationException("Cette session de caisse est deja cloturee.");
        }

        var payments = await _paymentRepository.GetByCashSessionIdAsync(session.Id, cancellationToken);
        var expenses = await _expenseRepository.GetByCashSessionIdAsync(session.Id, cancellationToken);

        var expectedAmount = CashCalculator.GetExpectedAmount(session.OpeningAmount, payments, expenses);
        var discrepancy = CashCalculator.GetDiscrepancy(request.CountedAmount, expectedAmount);

        session.ClosedAt = DateTime.UtcNow;
        session.CountedAmount = request.CountedAmount;
        session.ExpectedAmount = expectedAmount;
        session.Discrepancy = discrepancy;
        session.ClosedByUserId = request.ClosedByUserId;
        session.CloseComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        session.Status = CashSessionStatus.Closed;

        await _cashSessionRepository.UpdateAsync(session, cancellationToken);
        return ToDto(session);
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
