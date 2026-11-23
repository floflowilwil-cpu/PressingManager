using System.Globalization;
using System.Text;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.Core.Common;

/// <summary>
/// Generation des numeros de recus, libelles francais et formatage des justificatifs de paiement.
/// Classe pure sans dependance UI ni base de donnees.
/// </summary>
public static class PaymentReceiptGenerator
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Genere le numero officiel d'un recu : REC-YYYYMMDD-0001.</summary>
    public static string FormatReceiptNumber(DateTime paidAt, int paymentId)
    {
        if (paymentId < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paymentId),
                "L'identifiant du paiement doit etre superieur ou egal a 1.");
        }

        return $"REC-{paidAt:yyyyMMdd}-{paymentId:D4}";
    }

    /// <summary>Libelle francais du mode de paiement.</summary>
    public static string GetMethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Espèces",
        PaymentMethod.MtnMobileMoney => "MTN Mobile Money",
        PaymentMethod.OrangeMoney => "Orange Money",
        PaymentMethod.Other => "Autre",
        _ => method.ToString()
    };

    /// <summary>Libelle francais du statut d'un paiement.</summary>
    public static string GetStatusLabel(PaymentStatus status) => status switch
    {
        PaymentStatus.Valid => "Validé",
        PaymentStatus.Cancelled => "Annulé",
        PaymentStatus.Refunded => "Remboursé",
        _ => status.ToString()
    };

    /// <summary>Construit les donnees completes d'un recu de paiement.</summary>
    public static PaymentReceiptDto BuildReceipt(
        PaymentDto payment,
        string orderNumber,
        string customerName,
        decimal orderTotal,
        decimal totalPaidOnOrder,
        string cashierName)
    {
        ArgumentNullException.ThrowIfNull(payment);

        var safeOrderNumber = string.IsNullOrWhiteSpace(orderNumber)
            ? $"CMD-{payment.OrderId:D4}"
            : orderNumber.Trim();
        var safeCustomerName = string.IsNullOrWhiteSpace(customerName)
            ? "Client"
            : customerName.Trim();
        var safeCashierName = string.IsNullOrWhiteSpace(cashierName)
            ? "Caissier"
            : cashierName.Trim();

        var remaining = Math.Max(0m, orderTotal - totalPaidOnOrder);

        return new PaymentReceiptDto(
            ReceiptNumber: FormatReceiptNumber(payment.PaidAt, payment.Id),
            PaymentId: payment.Id,
            OrderId: payment.OrderId,
            OrderNumber: safeOrderNumber,
            CustomerName: safeCustomerName,
            OrderTotal: orderTotal,
            AmountPaid: payment.Amount,
            TotalPaidOnOrder: totalPaidOnOrder,
            RemainingBalance: remaining,
            Method: payment.Method,
            MethodLabel: GetMethodLabel(payment.Method),
            Reference: payment.Reference,
            PaidAt: payment.PaidAt,
            CashierName: safeCashierName);
    }

    /// <summary>Genere un recu texte propre pour archivage ou impression ticket.</summary>
    public static string FormatTextReceipt(PaymentReceiptDto receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var sb = new StringBuilder();
        sb.AppendLine("========================================");
        sb.AppendLine("           PRESSING MANAGER             ");
        sb.AppendLine("       RECU OFFICIEL DE PAIEMENT        ");
        sb.AppendLine("========================================");
        sb.AppendLine($"Recu N°       : {receipt.ReceiptNumber}");
        sb.AppendLine($"Date          : {receipt.PaidAt.ToLocalTime():dd/MM/yyyy HH:mm}");
        sb.AppendLine($"Commande      : {receipt.OrderNumber}");
        sb.AppendLine($"Client        : {receipt.CustomerName}");
        sb.AppendLine($"Caissier      : {receipt.CashierName}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Mode          : {receipt.MethodLabel}");

        if (!string.IsNullOrWhiteSpace(receipt.Reference))
        {
            sb.AppendLine($"Reference     : {receipt.Reference}");
        }

        sb.AppendLine($"Total cmd     : {receipt.OrderTotal.ToString("N2", FrenchCulture)}");
        sb.AppendLine($"Montant paye  : {receipt.AmountPaid.ToString("N2", FrenchCulture)}");
        sb.AppendLine($"Total verse   : {receipt.TotalPaidOnOrder.ToString("N2", FrenchCulture)}");
        sb.AppendLine($"Reste a payer : {receipt.RemainingBalance.ToString("N2", FrenchCulture)}");
        sb.AppendLine($"Statut        : {(receipt.IsFullyPaid ? "SOLDE" : "PAIEMENT PARTIEL")}");
        sb.AppendLine("========================================");
        sb.AppendLine("       Merci pour votre confiance !     ");
        return sb.ToString();
    }
}
