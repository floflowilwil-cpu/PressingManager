namespace PressingManager.Core.Enums;

/// <summary>
/// Actions protegees de l'application. Le controle se fait dans les services metier
/// (via ICurrentUserContext.Require), pas seulement en masquant des boutons.
/// </summary>
public enum Permission
{
    ViewDashboard = 0,
    ManageUsers = 1,
    ViewAuditLog = 2,
    ViewReports = 3,
    ExportReports = 4,
    ManageCustomers = 5,
    ManageOrders = 6,
    ViewOrders = 7,
    UpdateOrderStatus = 8,
    CorrectOrderStatus = 9,
    RegisterPayments = 10,
    CloseCashSession = 11,
    RefundPayments = 12,
    ManageExpenses = 13,
    ManageServices = 14,
    ManageStock = 15,
    ViewStock = 16
}
