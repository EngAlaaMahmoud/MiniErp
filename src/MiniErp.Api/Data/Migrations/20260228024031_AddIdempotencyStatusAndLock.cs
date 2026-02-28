using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyStatusAndLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "IdempotencyKeys",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "IdempotencyKeys",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntil",
                table: "IdempotencyKeys",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "IdempotencyKeys",
                type: "int",
                nullable: false,
                defaultValue: 1);

            if (ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    UPDATE [IdempotencyKeys]
                    SET
                        [Status] = CASE
                            WHEN [CompletedAt] IS NULL OR [ResponseStatusCode] IS NULL OR [ResponseBody] IS NULL THEN 1
                            ELSE 2
                        END,
                        [AttemptCount] = CASE WHEN [AttemptCount] = 0 THEN 1 ELSE [AttemptCount] END
                    WHERE [Status] = 0;
                    """);
            }
            else if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    """
                    UPDATE "IdempotencyKeys"
                    SET
                        "Status" = CASE
                            WHEN "CompletedAt" IS NULL OR "ResponseStatusCode" IS NULL OR "ResponseBody" IS NULL THEN 1
                            ELSE 2
                        END,
                        "AttemptCount" = CASE WHEN "AttemptCount" = 0 THEN 1 ELSE "AttemptCount" END
                    WHERE "Status" = 0;
                    """);
            }

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_TenantId_Status_LockedUntil",
                table: "IdempotencyKeys",
                columns: new[] { "TenantId", "Status", "LockedUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyKeys_TenantId_Status_LockedUntil",
                table: "IdempotencyKeys");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "IdempotencyKeys");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "IdempotencyKeys");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "IdempotencyKeys");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "IdempotencyKeys");
        }
    }
}
