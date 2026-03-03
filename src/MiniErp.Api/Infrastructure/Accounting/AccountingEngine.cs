using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;

namespace MiniErp.Api.Infrastructure.Accounting;

public static class AccountingEngine
{
    public static class Codes
    {
        public const string Cash = "1000";
        public const string Bank = "1010";
        public const string VisaClearing = "1020";
        public const string Inventory = "1100";
        public const string AccountsReceivable = "1200";
        public const string InputVat = "1300";
        public const string AccountsPayable = "2000";
        public const string OutputVat = "2100";
        public const string SalesRevenue = "4000";
        public const string SalesReturns = "4100";
    }

    public sealed record Line(string AccountCode, decimal Debit, decimal Credit, Guid? CustomerId = null, Guid? SupplierId = null, string? Note = null);

    public static async Task<(bool Ok, string? Error)> TryAddJournalEntryAsync(
        AppDbContext db,
        Guid tenantId,
        Guid branchId,
        DateTimeOffset at,
        string sourceType,
        Guid sourceId,
        string? description,
        IReadOnlyList<Line> lines,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new ArgumentException("sourceType required.", nameof(sourceType));
        }

        if (lines.Count == 0)
        {
            return (false, "JOURNAL_LINES_REQUIRED");
        }

        var debit = lines.Sum(x => x.Debit);
        var credit = lines.Sum(x => x.Credit);
        if (debit <= 0 || credit <= 0 || decimal.Round(debit - credit, 4) != 0m)
        {
            return (false, "JOURNAL_NOT_BALANCED");
        }

        var codes = lines.Select(x => x.AccountCode).Distinct(StringComparer.Ordinal).ToArray();
        var accounts = await db.ChartAccounts
            .AsNoTracking()
            .Where(x => codes.Contains(x.Code) && x.IsActive && x.IsPosting)
            .Select(x => new { x.Code, x.Id })
            .ToListAsync(ct);

        if (accounts.Count != codes.Length)
        {
            return (false, "ACCOUNTING_NOT_CONFIGURED");
        }

        var accountIdByCode = accounts.ToDictionary(x => x.Code, x => x.Id, StringComparer.Ordinal);

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
            At = at,
            SourceType = sourceType,
            SourceId = sourceId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            TotalDebit = debit,
            TotalCredit = credit,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.JournalEntries.Add(entry);

        var lineEntities = lines.Select(x => new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryId = entry.Id,
            AccountId = accountIdByCode[x.AccountCode],
            Debit = x.Debit,
            Credit = x.Credit,
            CustomerId = x.CustomerId,
            SupplierId = x.SupplierId,
            Note = x.Note
        });
        db.JournalEntryLines.AddRange(lineEntities);

        return (true, null);
    }
}

