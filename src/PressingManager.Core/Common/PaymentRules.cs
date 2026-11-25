using PressingManager.Core.Entities;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Common;

/// <summary>
/// Regles metier pures liees aux paiements (solde, trop-percu, doublon Mobile Money,
/// caisse ouverte et motif d'annulation/remboursement). Aucune dependance a la base de donnees.
/// </summary>
public static class PaymentRules
{
    /// <summary>Indique si le mode de paiement exige un numero de transaction unique.</summary>
    public static bool RequiresReference(PaymentMethod method) =>
        method is PaymentMethod.MtnMobileMoney or PaymentMethod.OrangeMoney;

    /// <summary>Indique si le mode de paiement exige une session de caisse ouverte.</summary>
    public static bool RequiresCashSession(PaymentMethod method) =>
        method == PaymentMethod.Cash;

    /// <summary>Total des paiements valides encaisses (ignore les paiements annules ou rembourses).</summary>
    public static decimal GetPaidAmount(IEnumerable<Payment> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);
        return payments
            .Where(p => p.Status == PaymentStatus.Valid)
            .Sum(p => p.Amount);
    }

    /// <summary>Solde restant a payer pour une commande.</summary>
    public static decimal GetRemainingBalance(decimal orderTotal, IEnumerable<Payment> payments) =>
        orderTotal - GetPaidAmount(payments);

    /// <summary>
    /// Valide qu'un nouveau paiement respecte toutes les regles metier avant enregistrement.
    /// Leve <see cref="InvalidOperationException"/> si une regle est violee.
    /// </summary>
    public static void ValidatePayment(
        decimal orderTotal,
        IEnumerable<Payment> existingPayments,
        decimal amount,
        PaymentMethod method,
        string? reference,
        bool hasOpenCashSession)
    {
        ArgumentNullException.ThrowIfNull(existingPayments);

        if (orderTotal < 0m)
        {
            throw new InvalidOperationException("Le montant total de la commande ne peut pas etre negatif.");
        }

        if (amount <= 0m)
        {
            throw new InvalidOperationException("Le montant du paiement doit etre strictement positif.");
        }

        var paymentList = existingPayments as ICollection<Payment> ?? existingPayments.ToList();
        var remainingBalance = GetRemainingBalance(orderTotal, paymentList);

        if (amount > remainingBalance)
        {
            throw new InvalidOperationException(
                $"Le montant saisi ({amount}) depasse le solde restant ({remainingBalance}).");
        }

        if (RequiresReference(method))
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new InvalidOperationException(
                    "Une reference de transaction est obligatoire pour un paiement Mobile Money.");
            }
        }

        if (RequiresCashSession(method) && !hasOpenCashSession)
        {
            throw new InvalidOperationException(
                "Impossible d'enregistrer un paiement en especes : aucune session de caisse n'est ouverte.");
        }
    }

    /// <summary>
    /// Valide qu'un paiement peut etre annule ou rembourse.
    /// Leve <see cref="InvalidOperationException"/> si le statut n'est pas valide ou si le motif manque.
    /// </summary>
    public static void ValidateCancellationOrRefund(Payment payment, string? reason)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (payment.Status != PaymentStatus.Valid)
        {
            throw new InvalidOperationException(
                "Seul un paiement au statut Valid peut etre annule ou rembourse.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException(
                "Un motif est obligatoire pour annuler ou rembourser un paiement.");
        }
    }
}
