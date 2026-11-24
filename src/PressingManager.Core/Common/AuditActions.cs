namespace PressingManager.Core.Common;

/// <summary>
/// Noms d'actions du journal d'audit. Les modules des autres membres les utilisent
/// via IAuditService (ex. remboursement, annulation, changement de tarif).
/// </summary>
public static class AuditActions
{
    // Authentification et comptes (module Personne 5)
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string AccountLocked = "AccountLocked";
    public const string Logout = "Logout";
    public const string InitialAdminCreated = "InitialAdminCreated";
    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";
    public const string UserActivated = "UserActivated";
    public const string UserDeactivated = "UserDeactivated";
    public const string PasswordReset = "PasswordReset";
    public const string PasswordChanged = "PasswordChanged";
    public const string ReportExported = "ReportExported";

    // Operations sensibles des autres modules (documentation, section 11)
    public const string PaymentRefunded = "PaymentRefunded";
    public const string PaymentModified = "PaymentModified";
    public const string OrderCancelled = "OrderCancelled";
    public const string OrderStatusCorrected = "OrderStatusCorrected";
    public const string PriceChanged = "PriceChanged";
    public const string CashSessionClosed = "CashSessionClosed";
}
