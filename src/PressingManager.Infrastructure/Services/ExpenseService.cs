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
/// Service de gestion des depenses du pressing (creation, recherche et annulation tracee).
/// </summary>
public sealed class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ICurrentUserContext? _session;
    private readonly IAuditService? _audit;
    private readonly TimeProvider _time;

    [ActivatorUtilitiesConstructor]
    public ExpenseService(
        IDbContextFactory<PressingDbContext> dbFactory,
        ICurrentUserContext session,
        IAuditService audit,
        TimeProvider time)
        : this(
            new EfExpenseRepository(dbFactory),
            new EfCashSessionRepository(dbFactory),
            session ?? throw new ArgumentNullException(nameof(session)),
            audit ?? throw new ArgumentNullException(nameof(audit)),
            time ?? throw new ArgumentNullException(nameof(time)))
    {
    }

    public ExpenseService(
        IExpenseRepository expenseRepository,
        ICashSessionRepository cashSessionRepository,
        ICurrentUserContext? session = null,
        IAuditService? audit = null,
        TimeProvider? time = null)
    {
        _expenseRepository = expenseRepository ?? throw new ArgumentNullException(nameof(expenseRepository));
        _cashSessionRepository = cashSessionRepository ?? throw new ArgumentNullException(nameof(cashSessionRepository));
        _session = session;
        _audit = audit;
        _time = time ?? TimeProvider.System;
    }

    public async Task<ExpenseDto> CreateAsync(
        CreateExpenseDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _session?.Require(Permission.ManageExpenses);

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new InvalidOperationException("La categorie de la depense est obligatoire.");
        }

        if (request.Amount <= 0m)
        {
            throw new InvalidOperationException("Le montant de la depense doit etre strictement positif.");
        }

        CashSession? openSession = null;
        if (PaymentRules.RequiresCashSession(request.Method))
        {
            openSession = await _cashSessionRepository.GetOpenSessionAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Impossible d'enregistrer une depense en especes : aucune session de caisse n'est ouverte.");
        }

        var userId = _session?.CurrentUser?.Id ?? request.CreatedByUserId;
        var expense = new Expense
        {
            Date = request.Date,
            Category = request.Category.Trim(),
            Amount = request.Amount,
            Method = request.Method,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedByUserId = userId,
            CashSessionId = openSession?.Id
        };

        await _expenseRepository.AddAsync(expense, cancellationToken);
        return ToDto(expense);
    }

    public async Task<IReadOnlyList<ExpenseDto>> SearchAsync(
        DateTime from,
        DateTime to,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            throw new InvalidOperationException(
                "La date de debut doit etre anterieure ou egale a la date de fin.");
        }

        var expenses = await _expenseRepository.SearchAsync(from, to, category, cancellationToken);
        return expenses.Select(ToDto).ToList();
    }

    public async Task CancelAsync(
        int expenseId,
        string reason,
        int cancelledByUserId,
        CancellationToken cancellationToken = default)
    {
        _session?.Require(Permission.ManageExpenses);

        var expense = await _expenseRepository.GetByIdAsync(expenseId, cancellationToken)
            ?? throw new InvalidOperationException($"Depense #{expenseId} introuvable.");

        if (expense.IsCancelled)
        {
            throw new InvalidOperationException("Cette depense est deja annulee.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException(
                "Un motif est obligatoire pour annuler une depense.");
        }

        var userId = _session?.CurrentUser?.Id ?? cancelledByUserId;
        expense.IsCancelled = true;
        expense.CancelReason = reason.Trim();
        expense.CancelledByUserId = userId;
        expense.CancelledAt = _time.GetUtcNow().UtcDateTime;

        await _expenseRepository.UpdateAsync(expense, cancellationToken);

        if (_audit is not null)
        {
            await _audit.LogAsync(
                "ExpenseCancelled",
                nameof(Expense),
                expense.Id.ToString(),
                $"Annulation depense ({expense.Amount}) : {expense.CancelReason}",
                userId,
                cancellationToken);
        }
    }

    private static ExpenseDto ToDto(Expense expense) => new(
        expense.Id,
        expense.Date,
        expense.Category,
        expense.Amount,
        expense.Method,
        expense.Comment,
        expense.CreatedByUserId,
        expense.CashSessionId,
        expense.IsCancelled,
        expense.CancelReason);
}
