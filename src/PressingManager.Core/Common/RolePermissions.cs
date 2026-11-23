using PressingManager.Core.Enums;

namespace PressingManager.Core.Common;

/// <summary>
/// Matrice role -> permissions (documentation, section 3). Classe pure, facile a tester.
/// Les permissions exactes doivent etre validees par l'equipe : tout se change ici, en un seul endroit.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlySet<Permission> None = new HashSet<Permission>();

    private static readonly IReadOnlyDictionary<UserRole, IReadOnlySet<Permission>> Map =
        new Dictionary<UserRole, IReadOnlySet<Permission>>
        {
            [UserRole.Administrator] = new HashSet<Permission>(Enum.GetValues<Permission>()),

            [UserRole.FrontDesk] = new HashSet<Permission>
            {
                Permission.ViewDashboard, Permission.ManageCustomers,
                Permission.ManageOrders, Permission.ViewOrders
            },

            [UserRole.Cashier] = new HashSet<Permission>
            {
                Permission.ViewDashboard, Permission.ViewOrders,
                Permission.RegisterPayments, Permission.CloseCashSession
            },

            [UserRole.Processor] = new HashSet<Permission>
            {
                Permission.ViewOrders, Permission.UpdateOrderStatus
            },

            [UserRole.StockManager] = new HashSet<Permission>
            {
                Permission.ViewStock, Permission.ManageStock
            }
        };

    public static IReadOnlySet<Permission> GetPermissions(UserRole role) =>
        Map.TryGetValue(role, out var permissions) ? permissions : None;

    public static bool Has(UserRole role, Permission permission) =>
        GetPermissions(role).Contains(permission);
}
