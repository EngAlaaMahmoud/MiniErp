using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalAndTaxLedgerAndSaleQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrCodeBase64",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalDebit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalCredit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxLedger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    TaxRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxPercent = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RefType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxLedger", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_TaxRegistrationNo",
                table: "Suppliers",
                columns: new[] { "TenantId", "TaxRegistrationNo" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_TaxRegistrationNo",
                table: "Customers",
                columns: new[] { "TenantId", "TaxRegistrationNo" });

            // Enforce uniqueness only for NOT NULL values (allow multiple NULLs).
            // SQL Server unique index treats NULL as a value unless filtered.
            if (ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    DROP INDEX IF EXISTS [IX_Suppliers_TenantId_TaxRegistrationNo] ON [Suppliers];
                    CREATE UNIQUE INDEX [IX_Suppliers_TenantId_TaxRegistrationNo]
                    ON [Suppliers] ([TenantId], [TaxRegistrationNo])
                    WHERE [TaxRegistrationNo] IS NOT NULL;

                    DROP INDEX IF EXISTS [IX_Customers_TenantId_TaxRegistrationNo] ON [Customers];
                    CREATE UNIQUE INDEX [IX_Customers_TenantId_TaxRegistrationNo]
                    ON [Customers] ([TenantId], [TaxRegistrationNo])
                    WHERE [TaxRegistrationNo] IS NOT NULL;
                    """);
            }
            else if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    DROP INDEX IF EXISTS "IX_Suppliers_TenantId_TaxRegistrationNo";
                    CREATE UNIQUE INDEX "IX_Suppliers_TenantId_TaxRegistrationNo"
                    ON "Suppliers" ("TenantId", "TaxRegistrationNo")
                    WHERE "TaxRegistrationNo" IS NOT NULL;

                    DROP INDEX IF EXISTS "IX_Customers_TenantId_TaxRegistrationNo";
                    CREATE UNIQUE INDEX "IX_Customers_TenantId_TaxRegistrationNo"
                    ON "Customers" ("TenantId", "TaxRegistrationNo")
                    WHERE "TaxRegistrationNo" IS NOT NULL;
                    """);
            }

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_BranchId_At",
                table: "JournalEntries",
                columns: new[] { "TenantId", "BranchId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_SourceType_SourceId",
                table: "JournalEntries",
                columns: new[] { "TenantId", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_TenantId_AccountId",
                table: "JournalEntryLines",
                columns: new[] { "TenantId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_TenantId_JournalEntryId",
                table: "JournalEntryLines",
                columns: new[] { "TenantId", "JournalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxLedger_TenantId_BranchId_At",
                table: "TaxLedger",
                columns: new[] { "TenantId", "BranchId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxLedger_TenantId_RefType_RefId_Type_TaxRateId_TaxPercent",
                table: "TaxLedger",
                columns: new[] { "TenantId", "RefType", "RefId", "Type", "TaxRateId", "TaxPercent" },
                unique: true,
                filter: ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
                    ? "[TaxRateId] IS NOT NULL"
                    : ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
                        ? "\"TaxRateId\" IS NOT NULL"
                        : null);

            migrationBuilder.CreateIndex(
                name: "IX_TaxLedger_TenantId_Type_At",
                table: "TaxLedger",
                columns: new[] { "TenantId", "Type", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "JournalEntryLines");

            migrationBuilder.DropTable(
                name: "TaxLedger");

            if (ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    DROP INDEX IF EXISTS [IX_Suppliers_TenantId_TaxRegistrationNo] ON [Suppliers];
                    DROP INDEX IF EXISTS [IX_Customers_TenantId_TaxRegistrationNo] ON [Customers];
                    """);
            }
            else if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    DROP INDEX IF EXISTS "IX_Suppliers_TenantId_TaxRegistrationNo";
                    DROP INDEX IF EXISTS "IX_Customers_TenantId_TaxRegistrationNo";
                    """);
            }
            else
            {
                migrationBuilder.DropIndex(
                    name: "IX_Suppliers_TenantId_TaxRegistrationNo",
                    table: "Suppliers");

                migrationBuilder.DropIndex(
                    name: "IX_Customers_TenantId_TaxRegistrationNo",
                    table: "Customers");
            }

            migrationBuilder.DropColumn(
                name: "QrCodeBase64",
                table: "Sales");
        }
    }
}
