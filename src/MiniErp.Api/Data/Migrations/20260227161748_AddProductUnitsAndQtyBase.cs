using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductUnitsAndQtyBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductUnitId",
                table: "SaleItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QtyBase",
                table: "SaleItems",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitFactor",
                table: "SaleItems",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "SaleItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductUnitId",
                table: "ReturnItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QtyBase",
                table: "ReturnItems",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitFactor",
                table: "ReturnItems",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "ReturnItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductUnitId",
                table: "PurchaseItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QtyBase",
                table: "PurchaseItems",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitFactor",
                table: "PurchaseItems",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "PurchaseItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductUnits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_TenantId_ProductId",
                table: "ProductUnits",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductUnits_TenantId_ProductId_Name",
                table: "ProductUnits",
                columns: new[] { "TenantId", "ProductId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductUnits");

            migrationBuilder.DropColumn(
                name: "ProductUnitId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "QtyBase",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ProductUnitId",
                table: "ReturnItems");

            migrationBuilder.DropColumn(
                name: "QtyBase",
                table: "ReturnItems");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "ReturnItems");

            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "ReturnItems");

            migrationBuilder.DropColumn(
                name: "ProductUnitId",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "QtyBase",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "PurchaseItems");
        }
    }
}
