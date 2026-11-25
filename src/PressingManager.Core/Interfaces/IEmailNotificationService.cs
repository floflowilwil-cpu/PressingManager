using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>Envoi automatique des recus PDF et notifications de statut par e-mail (ou archive locale .eml).</summary>
public interface IEmailNotificationService
{
    Task<EmailDeliveryResult> SendPaymentReceiptAsync(
        string recipientEmail,
        PaymentReceiptDto receipt,
        byte[] pdfBytes,
        bool isEnglish = false,
        CancellationToken cancellationToken = default);

    Task<EmailDeliveryResult> SendOrderStatusChangedAsync(
        string recipientEmail,
        OrderStatusEmailDto statusChange,
        bool isEnglish = false,
        CancellationToken cancellationToken = default);

    Task<EmailDeliveryResult> SendTestEmailAsync(
        string recipientEmail,
        bool isEnglish = false,
        CancellationToken cancellationToken = default);
}
