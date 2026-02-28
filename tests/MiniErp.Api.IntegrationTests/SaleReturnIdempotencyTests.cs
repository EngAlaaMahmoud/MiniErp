using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Pos;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Services;
using Xunit;

namespace MiniErp.Api.IntegrationTests;

public sealed class SaleReturnIdempotencyTests : IClassFixture<MiniErpApiFixture>
{
    private readonly MiniErpApiFixture _fx;

    public SaleReturnIdempotencyTests(MiniErpApiFixture fx) => _fx = fx;

    [SkippableFact]
    public async Task Sale_same_idempotency_key_returns_same_response_and_no_duplicate_ledger()
    {
        Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Database not available.");

        var req = new CreateSaleRequest(
            TestConstants.BranchId,
            CustomerId: null,
            Items: new[]
            {
                new SaleItemRequest(TestConstants.MilkProductId, ProductUnitId: null, Qty: 1m, UnitPrice: 30m, Discount: 0m)
            },
            Payments: new[]
            {
                new PaymentRequest("Cash", 30m)
            },
            Note: null);

        var first = await PostSaleAsync(req, "sale-idem-1");
        var second = await PostSaleAsync(req, "sale-idem-1");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(first.Body.SaleId, second.Body.SaleId);
        Assert.Equal(first.Body.SaleNo, second.Body.SaleNo);
        Assert.Equal(first.Body.Total, second.Body.Total);

        await using var db = _fx.CreateDbContext();

        var salesCount = await db.Sales.CountAsync(x => x.Id == first.Body.SaleId);
        Assert.Equal(1, salesCount);

        var ledgerCount = await db.StockLedgers.CountAsync(x => x.RefId == first.Body.SaleId && x.Reason == StockLedgerReason.Sale);
        Assert.Equal(1, ledgerCount);

        var itemsCount = await db.SaleItems.CountAsync(x => x.SaleId == first.Body.SaleId);
        Assert.Equal(1, itemsCount);

        var paymentsCount = await db.Payments.CountAsync(x => x.SaleId == first.Body.SaleId);
        Assert.Equal(1, paymentsCount);

        var cashCount = await db.CashTxns.CountAsync(x => x.RefType == "Sale" && x.RefId == first.Body.SaleId);
        Assert.Equal(1, cashCount);

        var printCount = await db.PrintJobs.CountAsync(x => x.RefType == "Sale" && x.RefId == first.Body.SaleId);
        Assert.Equal(1, printCount);
    }

    [SkippableFact]
    public async Task Return_same_idempotency_key_returns_same_response_and_no_duplicate_ledger()
    {
        Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Database not available.");

        var saleReq = new CreateSaleRequest(
            TestConstants.BranchId,
            CustomerId: null,
            Items: new[]
            {
                new SaleItemRequest(TestConstants.MilkProductId, ProductUnitId: null, Qty: 2m, UnitPrice: 30m, Discount: 0m)
            },
            Payments: new[]
            {
                new PaymentRequest("Cash", 60m)
            },
            Note: null);

        var sale = await PostSaleAsync(saleReq, "sale-idem-for-return");

        var retReq = new CreateReturnRequest(
            TestConstants.BranchId,
            OrigSaleId: sale.Body.SaleId,
            Items: new[]
            {
                new ReturnItemRequest(TestConstants.MilkProductId, ProductUnitId: null, Qty: 1m, UnitPrice: 30m, Discount: 0m)
            },
            RefundMethod: "Cash",
            Note: null);

        var first = await PostReturnAsync(retReq, "ret-idem-1");
        var second = await PostReturnAsync(retReq, "ret-idem-1");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(first.Body.ReturnId, second.Body.ReturnId);
        Assert.Equal(first.Body.ReturnNo, second.Body.ReturnNo);

        await using var db = _fx.CreateDbContext();
        var returnsCount = await db.Returns.CountAsync(x => x.Id == first.Body.ReturnId);
        Assert.Equal(1, returnsCount);

        var ledgerCount = await db.StockLedgers.CountAsync(x => x.RefId == first.Body.ReturnId && x.Reason == StockLedgerReason.Return);
        Assert.Equal(1, ledgerCount);

        var itemsCount = await db.ReturnItems.CountAsync(x => x.ReturnId == first.Body.ReturnId);
        Assert.Equal(1, itemsCount);

        var cashCount = await db.CashTxns.CountAsync(x => x.RefType == "Return" && x.RefId == first.Body.ReturnId);
        Assert.Equal(1, cashCount);

        var printCount = await db.PrintJobs.CountAsync(x => x.RefType == "Return" && x.RefId == first.Body.ReturnId);
        Assert.Equal(1, printCount);
    }

    [SkippableFact]
    public async Task StockBalance_concurrent_sales_are_atomic()
    {
        Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Database not available.");

        await using (var db = _fx.CreateDbContext())
        {
            var hasCounter = await db.Counters.AnyAsync(x => x.TenantId == TestConstants.TenantId && x.Name == "sale_no");
            if (!hasCounter)
            {
                db.Counters.Add(new MiniErp.Api.Domain.Counter { TenantId = TestConstants.TenantId, Name = "sale_no", NextValue = 2 });
            }

            var existing = await db.StockBalances.SingleOrDefaultAsync(x => x.TenantId == TestConstants.TenantId && x.BranchId == TestConstants.BranchId && x.ProductId == TestConstants.MilkProductId);
            if (existing is null)
            {
                db.StockBalances.Add(new MiniErp.Api.Domain.StockBalance
                {
                    TenantId = TestConstants.TenantId,
                    BranchId = TestConstants.BranchId,
                    ProductId = TestConstants.MilkProductId,
                    Qty = 1000m
                });
            }
            else
            {
                existing.Qty = 1000m;
            }

            await db.SaveChangesAsync();
        }

        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            var req = new CreateSaleRequest(
                TestConstants.BranchId,
                CustomerId: null,
                Items: new[] { new SaleItemRequest(TestConstants.MilkProductId, ProductUnitId: null, Qty: 1m, UnitPrice: 30m, Discount: 0m) },
                Payments: new[] { new PaymentRequest("Cash", 30m) },
                Note: null);

            var res = await PostSaleAsync(req, $"sale-conc-{i}");
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            return res.Body.SaleId;
        });

        var saleIds = await Task.WhenAll(tasks);

        await using var check = _fx.CreateDbContext();
        var balance = await check.StockBalances.SingleAsync(x => x.TenantId == TestConstants.TenantId && x.BranchId == TestConstants.BranchId && x.ProductId == TestConstants.MilkProductId);
        Assert.Equal(980m, balance.Qty);

        var salesCount = await check.Sales.CountAsync(x => saleIds.Contains(x.Id));
        Assert.Equal(20, salesCount);

        var ledgersCount = await check.StockLedgers.CountAsync(x => saleIds.Contains(x.RefId) && x.Reason == StockLedgerReason.Sale);
        Assert.Equal(20, ledgersCount);
    }

    [SkippableFact]
    public async Task Sale_in_progress_returns_409_then_stale_reclaim_executes()
    {
        Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Database not available.");

        var req = new CreateSaleRequest(
            TestConstants.BranchId,
            CustomerId: null,
            Items: new[]
            {
                new SaleItemRequest(TestConstants.MilkProductId, ProductUnitId: null, Qty: 1m, UnitPrice: 30m, Discount: 0m)
            },
            Payments: new[]
            {
                new PaymentRequest("Cash", 30m)
            },
            Note: null);

        var key = "sale-inprog-http-1";
        var operationId = Guid.NewGuid();
        var endpoint = "POST:/pos/sales";
        var hash = IdempotencyService.HashRequest(req);

        await using (var db = _fx.CreateDbContext())
        {
            db.IdempotencyKeys.Add(new IdempotencyKey
            {
                Id = operationId,
                TenantId = TestConstants.TenantId,
                DeviceId = TestConstants.DeviceId,
                Key = key,
                Endpoint = endpoint,
                RequestHash = hash,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = IdempotencyStatus.InProgress,
                LockedUntil = DateTimeOffset.UtcNow.AddMinutes(10),
                AttemptCount = 1
            });

            await db.SaveChangesAsync();
        }

        using (var msg = new HttpRequestMessage(HttpMethod.Post, "/pos/sales"))
        {
            msg.Headers.Add("Idempotency-Key", key);
            msg.Content = JsonContent.Create(req);
            var res = await _fx.Client.SendAsync(msg);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }

        await using (var db = _fx.CreateDbContext())
        {
            var row = await db.IdempotencyKeys
                .IgnoreQueryFilters()
                .SingleAsync(x => x.TenantId == TestConstants.TenantId && x.DeviceId == TestConstants.DeviceId && x.Key == key);

            row.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var created = await PostSaleAsync(req, key);
        Assert.Equal(operationId, created.Body.SaleId);

        var completed = await PostSaleAsync(req, key);
        Assert.Equal(created.Body.SaleId, completed.Body.SaleId);
        Assert.Equal(created.Body.SaleNo, completed.Body.SaleNo);
    }

    private async Task<(HttpStatusCode StatusCode, CreateSaleResponse Body)> PostSaleAsync(CreateSaleRequest req, string idempotencyKey)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/pos/sales");
        msg.Headers.Add("Idempotency-Key", idempotencyKey);
        msg.Content = JsonContent.Create(req);

        var res = await _fx.Client.SendAsync(msg);
        var body = await res.Content.ReadFromJsonAsync<CreateSaleResponse>();
        Assert.NotNull(body);
        return (res.StatusCode, body!);
    }

    private async Task<(HttpStatusCode StatusCode, CreateReturnResponse Body)> PostReturnAsync(CreateReturnRequest req, string idempotencyKey)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/pos/returns");
        msg.Headers.Add("Idempotency-Key", idempotencyKey);
        msg.Content = JsonContent.Create(req);

        var res = await _fx.Client.SendAsync(msg);
        var body = await res.Content.ReadFromJsonAsync<CreateReturnResponse>();
        Assert.NotNull(body);
        return (res.StatusCode, body!);
    }
}
