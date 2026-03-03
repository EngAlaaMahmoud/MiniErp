using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueProductSku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.CreateIndex(
                    name: "IX_Products_TenantId_Sku",
                    table: "Products",
                    columns: new[] { "TenantId", "Sku" },
                    unique: true,
                    filter: "[Sku] IS NOT NULL");
            }
            else if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Products_TenantId_Sku"
                    ON "Products" ("TenantId", "Sku")
                    WHERE "Sku" IS NOT NULL;
                    """);
            }
            else
            {
                migrationBuilder.CreateIndex(
                    name: "IX_Products_TenantId_Sku",
                    table: "Products",
                    columns: new[] { "TenantId", "Sku" },
                    unique: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    DROP INDEX IF EXISTS "IX_Products_TenantId_Sku";
                    """);
            }
            else
            {
                migrationBuilder.DropIndex(
                    name: "IX_Products_TenantId_Sku",
                    table: "Products");
            }
        }
    }
}
