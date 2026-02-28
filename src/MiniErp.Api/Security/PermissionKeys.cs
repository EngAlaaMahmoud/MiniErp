namespace MiniErp.Api.Security;

public static class PermissionKeys
{
    public const string DashboardView = "dashboard.view";
    public const string ReportsView = "reports.view";
    public const string AccountsView = "accounts.view";
    public const string AccountsManage = "accounts.manage";

    public const string CatalogView = "catalog.view";
    public const string CatalogEdit = "catalog.edit";

    public const string InventoryView = "inventory.view";
    public const string InventoryAdjust = "inventory.adjust";

    public const string TaxesView = "taxes.view";
    public const string TaxesManage = "taxes.manage";

    public const string CustomersView = "customers.view";
    public const string CustomersEdit = "customers.edit";

    public const string SuppliersView = "suppliers.view";
    public const string SuppliersEdit = "suppliers.edit";

    public const string PurchasesView = "purchases.view";
    public const string PurchasesCreate = "purchases.create";
    public const string PurchasesPrint = "purchases.print";

    public const string SalesView = "sales.view";
    public const string SalesCreate = "sales.create";
    public const string SalesPrint = "sales.print";
    public const string ReturnsCreate = "returns.create";

    public const string AdminUsers = "admin.users";
    public const string AdminPermissions = "admin.permissions";
}
