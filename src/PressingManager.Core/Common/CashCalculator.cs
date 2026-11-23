using PressingManager.Core.Entities;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Common;

/// <summary>
/// Calculs purs pour la caisse (montant theorique attendu et ecart a la cloture).
/// Aucune dependance a la base de donnees.
/// </summary>
public static class CashCalculator
{
    /// <summary>
    /// Calcule le montant theorique en especes attendu a la cloture :
    /// fonds d'ouverture + encaissements en especes valides - depenses en especes non annulees.
    /// Les paiements et depenses par Mobile Money ou autre mode sont ignores.
    /// </summary>
    public static decimal GetExpectedAmount(
        decimal openingAmount,
        IEnumerable<Payment> payments,
        IEnumerable<Expense> expenses)
    {
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(expenses);

        var cashPayments = payments
            .Where(p => p.Status == PaymentStatus.Valid && p.Method == PaymentMethod.Cash)
            .Sum(p => p.Amount);

        var cashExpenses = expenses
            .Where(e => !e.IsCancelled && e.Method == PaymentMethod.Cash)
            .Sum(e => e.Amount);

        return openingAmount + cashPayments - cashExpenses;
    }

    /// <summary>
    /// Calcule l'ecart de caisse (montant compte - montant attendu).
    /// Positif = excedent, negatif = manquant, zero = caisse juste.
    /// </summary>
    public static decimal GetDiscrepancy(decimal countedAmount, decimal expectedAmount) =>
        countedAmount - expectedAmount;
}
