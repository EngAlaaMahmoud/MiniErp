using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Infrastructure.Tax;

public static class TaxEngine
{
    public sealed record Line(Guid? TaxRateId, decimal TaxPercent, decimal Amount);

    public static void AddTaxLedgerEntries(
        AppDbContext db,
        Guid tenantId,
        Guid branchId,
        DateTimeOffset at,
        TaxLedgerType type,
        string refType,
        Guid refId,
        IReadOnlyList<Line> lines)
    {
        foreach (var line in lines)
        {
            if (line.Amount == 0)
            {
                continue;
            }

            db.TaxLedger.Add(new TaxLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = branchId,
                At = at,
                Type = type,
                TaxRateId = line.TaxRateId,
                TaxPercent = line.TaxPercent,
                Amount = line.Amount,
                RefType = refType,
                RefId = refId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }
}

