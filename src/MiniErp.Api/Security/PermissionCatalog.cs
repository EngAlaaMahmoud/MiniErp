namespace MiniErp.Api.Security;

public sealed record PermissionDef(string Key, string Group, string Name);

public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionDef> All =
    [
        new(PermissionKeys.DashboardView, "General", "Dashboard"),
        new(PermissionKeys.ReportsView, "General", "Reports"),
        new(PermissionKeys.AccountsView, "Accounts", "Accounts"),
        new(PermissionKeys.AccountsManage, "Accounts", "Manage chart of accounts"),

        new(PermissionKeys.CatalogView, "Catalog", "View products/categories"),
        new(PermissionKeys.CatalogEdit, "Catalog", "Edit products/categories"),

        new(PermissionKeys.InventoryView, "Inventory", "View inventory"),
        new(PermissionKeys.InventoryAdjust, "Inventory", "Inventory adjustments"),

        new(PermissionKeys.TaxesView, "Taxes", "View taxes"),
        new(PermissionKeys.TaxesManage, "Taxes", "Manage taxes"),

        new(PermissionKeys.CustomersView, "Parties", "View customers"),
        new(PermissionKeys.CustomersEdit, "Parties", "Manage customers"),

        new(PermissionKeys.SuppliersView, "Parties", "View suppliers"),
        new(PermissionKeys.SuppliersEdit, "Parties", "Manage suppliers"),

        new(PermissionKeys.PurchasesView, "Purchases", "View purchases"),
        new(PermissionKeys.PurchasesCreate, "Purchases", "Create purchases"),
        new(PermissionKeys.PurchasesPrint, "Purchases", "Print purchases"),

        new(PermissionKeys.SalesView, "Sales", "View sales"),
        new(PermissionKeys.SalesCreate, "Sales", "Create sales"),
        new(PermissionKeys.SalesPrint, "Sales", "Print sales"),
        new(PermissionKeys.ReturnsCreate, "Sales", "Create returns"),

        new(PermissionKeys.AdminUsers, "Admin", "User management"),
        new(PermissionKeys.AdminPermissions, "Admin", "Role permissions"),
    ];

    public static bool IsKnown(string key) => All.Any(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
}
