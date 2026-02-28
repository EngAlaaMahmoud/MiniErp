using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Data;

namespace MiniErp.Api.Infrastructure.Inventory;

public static class StockBalanceWriter
{
    public static async Task ApplyDeltaAsync(
        AppDbContext db,
        Guid tenantId,
        Guid branchId,
        Guid productId,
        decimal qtyDelta,
        CancellationToken ct)
    {
        var provider = db.Database.ProviderName ?? "";

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 MERGE [StockBalance] WITH (HOLDLOCK) AS target
                 USING (SELECT {tenantId} AS [TenantId], {branchId} AS [BranchId], {productId} AS [ProductId], {qtyDelta} AS [QtyDelta]) AS source
                 ON target.[TenantId] = source.[TenantId]
                    AND target.[BranchId] = source.[BranchId]
                    AND target.[ProductId] = source.[ProductId]
                 WHEN MATCHED THEN
                    UPDATE SET [Qty] = target.[Qty] + source.[QtyDelta]
                 WHEN NOT MATCHED THEN
                    INSERT ([TenantId], [BranchId], [ProductId], [Qty])
                    VALUES (source.[TenantId], source.[BranchId], source.[ProductId], source.[QtyDelta]);
                 """,
                ct);

            return;
        }

        // Postgres (Npgsql)
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "StockBalance" ("TenantId", "BranchId", "ProductId", "Qty")
             VALUES ({tenantId}, {branchId}, {productId}, {qtyDelta})
             ON CONFLICT ("TenantId", "BranchId", "ProductId")
             DO UPDATE SET "Qty" = "StockBalance"."Qty" + EXCLUDED."Qty";
             """,
            ct);
    }

    public static async Task ApplyDeltasAsync(
        AppDbContext db,
        Guid tenantId,
        Guid branchId,
        IReadOnlyList<(Guid ProductId, decimal QtyDelta)> deltas,
        CancellationToken ct)
    {
        foreach (var (productId, qtyDelta) in deltas)
        {
            if (productId == Guid.Empty || qtyDelta == 0)
            {
                continue;
            }

            await ApplyDeltaAsync(db, tenantId, branchId, productId, qtyDelta, ct);
        }
    }
}

