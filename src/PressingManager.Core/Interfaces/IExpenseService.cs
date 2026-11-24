using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>
/// Gestion des depenses du pressing (creation, recherche et annulation tracee).
/// </summary>
public interface IExpenseService
{
    /// <summary>
    /// Enregistre une depense. Si le mode est Cash, une session de caisse ouverte est requise.
    /// </summary>
    Task<ExpenseDto> CreateAsync(
        CreateExpenseDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Recherche les depenses sur une plage de dates, avec filtre optionnel sur la categorie.</summary>
    Task<IReadOnlyList<ExpenseDto>> SearchAsync(
        DateTime from,
        DateTime to,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>Annule une depense sans suppression physique, avec motif et tracabilite.</summary>
    Task CancelAsync(
        int expenseId,
        string reason,
        int cancelledByUserId,
        CancellationToken cancellationToken = default);
}
