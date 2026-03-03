using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryParent : Migration
    {
        private static string NotNullFilter(string activeProvider, string columnName)
        {
            if (activeProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                return $"[{columnName}] IS NOT NULL";
            }

            if (activeProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                return $"\"{columnName}\" IS NOT NULL";
            }

            return string.Empty;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaxLedger_TenantId_RefType_RefId_Type_TaxRateId_TaxPercent",
                table: "TaxLedger");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TenantId_TaxRegistrationNo",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_TaxRegistrationNo",
                table: "Customers");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxLedger_TenantId_RefType_RefId_Type_TaxRateId_TaxPercent",
                table: "TaxLedger",
                columns: new[] { "TenantId", "RefType", "RefId", "Type", "TaxRateId", "TaxPercent" },
                unique: true,
                filter: NotNullFilter(ActiveProvider, "TaxRateId").Length == 0 ? null : NotNullFilter(ActiveProvider, "TaxRateId"));

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_TaxRegistrationNo",
                table: "Suppliers",
                columns: new[] { "TenantId", "TaxRegistrationNo" },
                unique: true,
                filter: NotNullFilter(ActiveProvider, "TaxRegistrationNo").Length == 0 ? null : NotNullFilter(ActiveProvider, "TaxRegistrationNo"));

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_TaxRegistrationNo",
                table: "Customers",
                columns: new[] { "TenantId", "TaxRegistrationNo" },
                unique: true,
                filter: NotNullFilter(ActiveProvider, "TaxRegistrationNo").Length == 0 ? null : NotNullFilter(ActiveProvider, "TaxRegistrationNo"));

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId_ParentId",
                table: "Categories",
                columns: new[] { "TenantId", "ParentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaxLedger_TenantId_RefType_RefId_Type_TaxRateId_TaxPercent",
                table: "TaxLedger");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TenantId_TaxRegistrationNo",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_TaxRegistrationNo",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Categories_TenantId_ParentId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_TaxLedger_TenantId_RefType_RefId_Type_TaxRateId_TaxPercent",
                table: "TaxLedger",
                columns: new[] { "TenantId", "RefType", "RefId", "Type", "TaxRateId", "TaxPercent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_TaxRegistrationNo",
                table: "Suppliers",
                columns: new[] { "TenantId", "TaxRegistrationNo" },
                unique: true,
                filter: NotNullFilter(ActiveProvider, "TaxRegistrationNo").Length == 0 ? null : NotNullFilter(ActiveProvider, "TaxRegistrationNo"));

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_TaxRegistrationNo",
                table: "Customers",
                columns: new[] { "TenantId", "TaxRegistrationNo" },
                unique: true,
                filter: NotNullFilter(ActiveProvider, "TaxRegistrationNo").Length == 0 ? null : NotNullFilter(ActiveProvider, "TaxRegistrationNo"));
        }
    }
}
