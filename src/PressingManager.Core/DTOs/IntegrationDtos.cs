using PressingManager.Core.Enums;

namespace PressingManager.Core.DTOs;

/// <summary>Parametres d'envoi d'e-mails (SMTP configurable + copie locale .eml si desactive).</summary>
public sealed class EmailNotificationSettings
{
    public bool Enabled { get; set; }

    public string SmtpHost { get; set; } = "smtp.gmail.com";

    public int SmtpPort { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Pressing Manager";

    public bool SaveLocalCopyWhenDisabled { get; set; } = true;
}

/// <summary>Parametres de passerelle Mobile Money (Simulation ou API Campay MTN/Orange).</summary>
public sealed class MobileMoneySettings
{
    public string Provider { get; set; } = "Simulation";

    public string BaseUrl { get; set; } = "https://demo.campay.net/api";

    public string ApiToken { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 90;
}

public sealed class IntegrationSettings
{
    public EmailNotificationSettings Email { get; set; } = new();

    public MobileMoneySettings MobileMoney { get; set; } = new();
}

public sealed record EmailDeliveryResult(
    bool SentViaSmtp,
    bool SavedLocally,
    string? LocalFilePath,
    string? ErrorMessage)
{
    public bool Delivered => SentViaSmtp || SavedLocally;
}

public sealed record OrderStatusEmailDto(
    string OrderNumber,
    string CustomerName,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus,
    DateTime? ExpectedDate,
    string? Comment,
    IReadOnlyList<string> ItemSummaries);

public sealed record MobileMoneyChargeRequest(
    PaymentMethod Method,
    string PhoneNumber,
    decimal Amount,
    string OrderNumber,
    string Description);

public sealed record MobileMoneyChargeResult(
    bool Success,
    string? OperatorReference,
    bool IsSimulated,
    string StatusMessage);
