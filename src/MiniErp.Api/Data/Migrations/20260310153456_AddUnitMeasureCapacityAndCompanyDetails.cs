using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitMeasureCapacityAndCompanyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Capacity",
                table: "UnitMeasures",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "ProductCompanies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "ProductCompanies",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fax",
                table: "ProductCompanies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                table: "ProductCompanies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "ProductCompanies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "UnitMeasures");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "ProductCompanies");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "ProductCompanies");

            migrationBuilder.DropColumn(
                name: "Fax",
                table: "ProductCompanies");

            migrationBuilder.DropColumn(
                name: "Mobile",
                table: "ProductCompanies");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "ProductCompanies");
        }
    }
}
