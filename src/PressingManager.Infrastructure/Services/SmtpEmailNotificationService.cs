using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Envoi automatique des recus PDF et notifications de changement de statut par SMTP,
/// avec sauvegarde locale au format .eml dans %LocalAppData%\PressingManager\outbox lorsque SMTP n'est pas active.
/// </summary>
public sealed class SmtpEmailNotificationService : IEmailNotificationService
{
    private static readonly string DefaultOutboxDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PressingManager",
        "outbox");

    private readonly IIntegrationSettingsStore _settingsStore;
    private readonly string _outboxDirectory;

    [ActivatorUtilitiesConstructor]
    public SmtpEmailNotificationService(IIntegrationSettingsStore settingsStore)
        : this(settingsStore, DefaultOutboxDirectory)
    {
    }

    public SmtpEmailNotificationService(IIntegrationSettingsStore settingsStore, string outboxDirectory)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxDirectory);
        _outboxDirectory = outboxDirectory;
    }

    public async Task<EmailDeliveryResult> SendPaymentReceiptAsync(
        string recipientEmail,
        PaymentReceiptDto receipt,
        byte[] pdfBytes,
        bool isEnglish = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(pdfBytes);

        if (!TryNormalizeEmail(recipientEmail, out var normalizedRecipient))
        {
            return new EmailDeliveryResult(false, false, null, "Adresse e-mail invalide.");
        }

        var settings = _settingsStore.Load().Email;
        var subject = isEnglish
            ? $"Payment receipt {receipt.ReceiptNumber} — Order {receipt.OrderNumber}"
            : $"Votre reçu de paiement {receipt.ReceiptNumber} — Commande {receipt.OrderNumber}";

        var methodLabel = PaymentReceiptGenerator.GetMethodLabel(receipt.Method, isEnglish);
        var amountPaidText = MoneyFormatter.FormatFcfa(receipt.AmountPaid);
        var totalText = MoneyFormatter.FormatFcfa(receipt.OrderTotal);
        var balanceText = receipt.IsFullyPaid
            ? (isEnglish ? "Paid in full" : "Soldée")
            : MoneyFormatter.FormatFcfa(receipt.RemainingBalance);

        var bodyTitle = isEnglish ? "OFFICIAL PAYMENT RECEIPT" : "REÇU OFFICIEL DE PAIEMENT";
        var greeting = isEnglish
            ? $"Hello {Encode(receipt.CustomerName)},"
            : $"Bonjour {Encode(receipt.CustomerName)},";
        var intro = isEnglish
            ? $"Thank you for your payment. Please find your official PDF receipt attached (<strong>{Encode(receipt.ReceiptNumber)}</strong>)."
            : $"Merci pour votre règlement. Vous trouverez en pièce jointe votre reçu officiel au format PDF (<strong>{Encode(receipt.ReceiptNumber)}</strong>).";

        var rows = isEnglish
            ? $"""
               <tr><td style="padding:6px 0;color:#64748B;">Order</td><td style="padding:6px 0;font-weight:bold;text-align:right;">{Encode(receipt.OrderNumber)}</td></tr>
               <tr><td style="padding:6px 0;color:#64748B;">Payment method</td><td style="padding:6px 0;font-weight:bold;text-align:right;">{Encode(methodLabel)}</td></tr>
               <tr><td style="padding:6px 0;color:#64748B;">Amount paid</td><td style="padding:6px 0;font-weight:bold;color:#10B981;text-align:right;">{Encode(amountPaidText)}</td></tr>
               <tr><td style="padding:6px 0;color:#64748B;">Order total</td><td style="padding:6px 0;text-align:right;">{Encode(totalText)}</td></tr>
               <tr><td style="padding:6px 0;color:#64748B;">Remaining balance</td><td style="padding:6px 0;font-weight:bold;text-align:right;">{Encode(balanceText)}</td></tr>
               """
            : $"""
               <tr><td style="padding:6px 0;color:#64748B;">Commande</td><td style="padding:6px 0;font-weight:bold;text-align:right;">{Encode(receipt.OrderNumber)}</td></tr>
               <tr><td style="padding:6px 0;color:#64748B;">Mode de paiement</td><td style="padding:6px 0;font-weight:bold;text-align:right;">{Encode(methodLabel)}</td></tr>
               <tr><td style="padding:6px 0;color:#64748B;">Montant payé</td><td style="padding:6px 0;font-weight:bold;color:#10B981;text-align:right;">{Encode(amountPaidText)}</td></tr>
               <tr><td style="padding:6px 0;color:#64748B;">Total commande</td><td style="padding:6px 0;text-align:right;">{Encode(totalText)}</td></tr>
               <tr><td style="padding:6px 0;color:#64748B;">Reste à payer</td><td style="padding:6px 0;font-weight:bold;text-align:right;">{Encode(balanceText)}</td></tr>
               """;

        var htmlBody = WrapHtmlEmail(
            bodyTitle,
            greeting,
            $"<p>{intro}</p><table style=\"width:100%;border-collapse:collapse;background:#F8FAFC;border-radius:8px;padding:12px;\">{rows}</table>",
            isEnglish ? "Thank you for your trust!" : "Merci pour votre confiance !");

        using var message = CreateBaseMessage(settings, normalizedRecipient, subject, htmlBody);
        using var pdfStream = new MemoryStream(pdfBytes, writable: false);
        using var attachment = new Attachment(pdfStream, $"{receipt.ReceiptNumber}.pdf", MediaTypeNames.Application.Pdf);
        message.Attachments.Add(attachment);

        return await DeliverAsync(message, settings, receipt.ReceiptNumber, cancellationToken);
    }

    public async Task<EmailDeliveryResult> SendOrderStatusChangedAsync(
        string recipientEmail,
        OrderStatusEmailDto statusChange,
        bool isEnglish = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statusChange);

        if (!TryNormalizeEmail(recipientEmail, out var normalizedRecipient))
        {
            return new EmailDeliveryResult(false, false, null, "Adresse e-mail invalide.");
        }

        var settings = _settingsStore.Load().Email;
        var prevLabel = GetOrderStatusLabel(statusChange.PreviousStatus, isEnglish);
        var newLabel = GetOrderStatusLabel(statusChange.NewStatus, isEnglish);

        var subject = isEnglish
            ? $"Order {statusChange.OrderNumber} status: {newLabel}"
            : $"Mise à jour commande {statusChange.OrderNumber} : {newLabel}";

        var title = isEnglish ? "ORDER STATUS UPDATE" : "SUIVI DE VOTRE COMMANDE";
        var greeting = isEnglish
            ? $"Hello {Encode(statusChange.CustomerName)},"
            : $"Bonjour {Encode(statusChange.CustomerName)},";

        var readyBanner = statusChange.NewStatus == OrderStatus.ReadyForPickup
            ? $"<div style=\"background:#ECFDF5;border:1px solid #A7F3D0;color:#065F46;padding:12px;border-radius:8px;margin-bottom:14px;font-weight:bold;\">{Encode(isEnglish ? "Your items are ready for pickup!" : "Bonne nouvelle : vos articles sont prêts à être récupérés !")}</div>"
            : string.Empty;

        var statusLine = isEnglish
            ? $"Your order <strong>{Encode(statusChange.OrderNumber)}</strong> moved from <em>{Encode(prevLabel)}</em> to <strong style=\"color:#0F8F91;\">{Encode(newLabel)}</strong>."
            : $"Votre commande <strong>{Encode(statusChange.OrderNumber)}</strong> est passée de <em>{Encode(prevLabel)}</em> à <strong style=\"color:#0F8F91;\">{Encode(newLabel)}</strong>.";

        var expectedHtml = statusChange.ExpectedDate is { } expected
            ? $"<p><strong>{(isEnglish ? "Expected pickup date" : "Date prévue de retrait")} :</strong> {expected.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}</p>"
            : string.Empty;

        var commentHtml = !string.IsNullOrWhiteSpace(statusChange.Comment)
            ? $"<p><strong>{(isEnglish ? "Note" : "Commentaire")} :</strong> {Encode(statusChange.Comment)}</p>"
            : string.Empty;

        var itemsHtml = statusChange.ItemSummaries.Count > 0
            ? $"<p style=\"margin-bottom:4px;\"><strong>{(isEnglish ? "Items" : "Articles")} :</strong></p><ul style=\"margin-top:4px;\">{string.Join("", statusChange.ItemSummaries.Select(i => $"<li>{Encode(i)}</li>"))}</ul>"
            : string.Empty;

        var htmlBody = WrapHtmlEmail(
            title,
            greeting,
            $"{readyBanner}<p>{statusLine}</p>{expectedHtml}{commentHtml}{itemsHtml}",
            isEnglish ? "Pressing Manager — Customer Notification" : "Pressing Manager — Notification client");

        using var message = CreateBaseMessage(settings, normalizedRecipient, subject, htmlBody);
        return await DeliverAsync(message, settings, $"{statusChange.OrderNumber}-{statusChange.NewStatus}", cancellationToken);
    }

    public async Task<EmailDeliveryResult> SendTestEmailAsync(
        string recipientEmail,
        bool isEnglish = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeEmail(recipientEmail, out var normalizedRecipient))
        {
            return new EmailDeliveryResult(false, false, null, "Adresse e-mail invalide.");
        }

        var settings = _settingsStore.Load().Email;
        var subject = isEnglish
            ? "SMTP Test — Pressing Manager"
            : "Test d'envoi e-mail — Pressing Manager";

        var htmlBody = WrapHtmlEmail(
            isEnglish ? "EMAIL CONFIGURATION TEST" : "TEST DE CONFIGURATION E-MAIL",
            isEnglish ? "Hello," : "Bonjour,",
            $"<p>{Encode(isEnglish ? "Your email notification configuration is working properly." : "Votre configuration d'envoi automatique d'e-mails fonctionne correctement.")}</p>",
            "Pressing Manager");

        using var message = CreateBaseMessage(settings, normalizedRecipient, subject, htmlBody);
        return await DeliverAsync(message, settings, "SMTP-TEST", cancellationToken);
    }

    private async Task<EmailDeliveryResult> DeliverAsync(
        MailMessage message,
        EmailNotificationSettings settings,
        string fileTag,
        CancellationToken cancellationToken)
    {
        if (settings.Enabled
            && !string.IsNullOrWhiteSpace(settings.SmtpHost)
            && TryNormalizeEmail(settings.FromAddress, out _))
        {
            try
            {
                using var client = new SmtpClient(settings.SmtpHost.Trim(), settings.SmtpPort > 0 ? settings.SmtpPort : 587)
                {
                    EnableSsl = settings.UseSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                if (!string.IsNullOrWhiteSpace(settings.Username))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(settings.Username.Trim(), settings.Password ?? string.Empty);
                }

                await client.SendMailAsync(message, cancellationToken);
                return new EmailDeliveryResult(true, false, null, null);
            }
            catch (Exception ex) when (ex is SmtpException or InvalidOperationException or IOException)
            {
                return new EmailDeliveryResult(false, false, null, ex.Message);
            }
        }

        if (!settings.SaveLocalCopyWhenDisabled)
        {
            return new EmailDeliveryResult(false, false, null, "L'envoi SMTP est désactivé.");
        }

        try
        {
            Directory.CreateDirectory(_outboxDirectory);
            var tempPickupDir = Path.Combine(_outboxDirectory, ".tmp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempPickupDir);

            try
            {
                using var pickupClient = new SmtpClient
                {
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    PickupDirectoryLocation = tempPickupDir
                };

                await pickupClient.SendMailAsync(message, cancellationToken);

                var generated = Directory.GetFiles(tempPickupDir, "*.eml").FirstOrDefault();
                if (generated is null)
                {
                    return new EmailDeliveryResult(false, false, null, "Échec de création du fichier .eml local.");
                }

                var safeTag = SanitizeFileTag(fileTag);
                var targetPath = Path.Combine(_outboxDirectory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeTag}.eml");
                File.Move(generated, targetPath, overwrite: true);
                return new EmailDeliveryResult(false, true, targetPath, null);
            }
            finally
            {
                if (Directory.Exists(tempPickupDir))
                {
                    Directory.Delete(tempPickupDir, recursive: true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SmtpException)
        {
            return new EmailDeliveryResult(false, false, null, ex.Message);
        }
    }

    private static MailMessage CreateBaseMessage(
        EmailNotificationSettings settings,
        string recipientEmail,
        string subject,
        string htmlBody)
    {
        var fromAddress = TryNormalizeEmail(settings.FromAddress, out var cleanFrom)
            ? cleanFrom
            : (TryNormalizeEmail(settings.Username, out var cleanUser) ? cleanUser : "noreply@pressingmanager.local");
        var fromName = string.IsNullOrWhiteSpace(settings.FromName) ? "Pressing Manager" : settings.FromName.Trim();

        var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName, Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = htmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(recipientEmail));
        return message;
    }

    private static string WrapHtmlEmail(string headerTitle, string greeting, string contentHtml, string footer) =>
        $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family:'Segoe UI',Arial,sans-serif;background:#F3F6FB;margin:0;padding:24px;color:#1F2F46;">
          <div style="max-width:580px;margin:0 auto;background:#FFFFFF;border-radius:12px;overflow:hidden;border:1px solid #E2E8F0;">
            <div style="background:#0F2A4A;padding:18px 24px;color:#FFFFFF;">
              <div style="font-size:18px;font-weight:bold;letter-spacing:0.5px;">PRESSING MANAGER</div>
              <div style="font-size:12px;color:#14B8A6;font-weight:bold;margin-top:4px;">{Encode(headerTitle)}</div>
            </div>
            <div style="padding:24px;line-height:1.5;font-size:14px;">
              <p style="margin-top:0;">{greeting}</p>
              {contentHtml}
            </div>
            <div style="background:#F8FAFC;padding:14px 24px;text-align:center;font-size:12px;color:#64748B;border-top:1px solid #E2E8F0;">
              {Encode(footer)}
            </div>
          </div>
        </body>
        </html>
        """;

    private static string GetOrderStatusLabel(OrderStatus status, bool isEnglish) => status switch
    {
        OrderStatus.Received => isEnglish ? "Received" : "Reçu",
        OrderStatus.AwaitingProcessing => isEnglish ? "Awaiting processing" : "En attente de traitement",
        OrderStatus.Cleaning => isEnglish ? "Cleaning" : "En nettoyage",
        OrderStatus.Ironing => isEnglish ? "Ironing" : "En repassage",
        OrderStatus.ReadyForPickup => isEnglish ? "Ready for pickup" : "Prêt à récupérer",
        OrderStatus.Delivered => isEnglish ? "Delivered" : "Livré",
        OrderStatus.Cancelled => isEnglish ? "Cancelled" : "Annulé",
        _ => status.ToString()
    };

    private static bool TryNormalizeEmail(string? raw, out string normalized)
    {
        normalized = (raw ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return false;
        }

        var at = normalized.IndexOf('@');
        return MailAddress.TryCreate(normalized, out var parsed)
            && string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase)
            && at > 0
            && normalized.IndexOf('.', at) > at + 1
            && !normalized.EndsWith('.');
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    private static string SanitizeFileTag(string tag)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(tag.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "mail" : cleaned;
    }
}
