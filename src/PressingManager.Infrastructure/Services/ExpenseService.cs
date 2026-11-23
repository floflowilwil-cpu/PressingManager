using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Service de gestion des depenses du pressing (creation, recherche et annulation tracee).
/// </summary>
public sealed class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICashSessionRepository _cashSessionRepository;

    public ExpenseService(
        IExpenseRepository expenseRepository,
        ICashSessionRepository cashSessionRepository)
    {
        _expenseRepository = expenseRepository ?? throw new ArgumentNullException(nameof(expenseRepository));
        _cashSessionRepository = cashSessionRepository ?? throw new ArgumentNullException(nameof(cashSessionRepository));
    }

    public async Task<ExpenseDto> CreateAsync(
        CreateExpenseDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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

        var expense = new Expense
        {
            Date = request.Date,
            Category = request.Category.Trim(),
            Amount = request.Amount,
            Method = request.Method,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedByUserId = request.CreatedByUserId,
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

        expense.IsCancelled = true;
        expense.CancelReason = reason.Trim();
        expense.CancelledByUserId = cancelledByUserId;
        expense.CancelledAt = DateTime.UtcNow;

        await _expenseRepository.UpdateAsync(expense, cancellationToken);
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
