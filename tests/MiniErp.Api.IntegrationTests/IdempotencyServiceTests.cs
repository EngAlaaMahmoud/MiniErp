using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Services;
using Xunit;

namespace MiniErp.Api.IntegrationTests;

public sealed class IdempotencyServiceTests : IClassFixture<MiniErpApiFixture>
{
    private readonly MiniErpApiFixture _fx;

    public IdempotencyServiceTests(MiniErpApiFixture fx) => _fx = fx;

    [SkippableFact]
    public async Task In_progress_request_returns_conflict()
    {
        Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Database not available.");

        var service = new IdempotencyService(NullLogger<IdempotencyService>.Instance);
        var endpoint = "TEST:/idem";
        var key = Guid.NewGuid().ToString("N");
        var request = new { x = 1 };
        var hash = IdempotencyService.HashRequest(request);
        var ttl = TimeSpan.FromSeconds(10);

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<(int StatusCode, object Body)> SlowAction(Guid operationId, CancellationToken ct)
        {
            started.TrySetResult();
            await Task.Delay(1500, ct);
            return (StatusCodes.Status200OK, new { operationId });
        }

        var t1 = Task.Run(async () =>
        {
            await using var db1 = _fx.CreateDbContext();
            return await service.ExecuteAsync(db1, TestConstants.TenantId, TestConstants.DeviceId, endpoint, key, hash, SlowAction, ttl, CancellationToken.None);
        });

        await started.Task;

        await using var db2 = _fx.CreateDbContext();
        var r2 = await service.ExecuteAsync(db2, TestConstants.TenantId, TestConstants.DeviceId, endpoint, key, hash,
            async (operationId, ct) => (StatusCodes.Status200OK, (object)new { operationId }),
            ttl,
            CancellationToken.None);

        var conflict = Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(r2);
        Assert.NotNull(conflict.Value);

        await t1;
    }

    [SkippableFact]
    public async Task Stale_in_progress_is_reclaimed_and_completed()
    {
        Skip.IfNot(_fx.IsAvailable, _fx.UnavailableReason ?? "Database not available.");

        var service = new IdempotencyService(NullLogger<IdempotencyService>.Instance);
        var endpoint = "TEST:/stale";
        var key = Guid.NewGuid().ToString("N");
        var request = new { y = 2 };
        var hash = IdempotencyService.HashRequest(request);
        var ttl = TimeSpan.FromSeconds(5);

        Guid operationId;

        await using (var db = _fx.CreateDbContext())
        {
            operationId = Guid.NewGuid();
            db.IdempotencyKeys.Add(new IdempotencyKey
            {
                Id = operationId,
                TenantId = TestConstants.TenantId,
                DeviceId = TestConstants.DeviceId,
                Key = key,
                Endpoint = endpoint,
                RequestHash = hash,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                Status = IdempotencyStatus.InProgress,
                LockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1),
                AttemptCount = 1
            });
            await db.SaveChangesAsync();
        }

        await using (var db = _fx.CreateDbContext())
        {
            var r = await service.ExecuteAsync(db, TestConstants.TenantId, TestConstants.DeviceId, endpoint, key, hash,
                async (opId, ct) => (StatusCodes.Status200OK, (object)new { opId }),
                ttl,
                CancellationToken.None);

            Assert.IsType<Microsoft.AspNetCore.Mvc.ContentResult>(r);
        }

        await using (var check = _fx.CreateDbContext())
        {
            var row = await check.IdempotencyKeys.IgnoreQueryFilters().SingleAsync(x => x.TenantId == TestConstants.TenantId && x.DeviceId == TestConstants.DeviceId && x.Key == key);
            Assert.Equal(IdempotencyStatus.Completed, row.Status);
            Assert.True(row.AttemptCount >= 2);
            Assert.Null(row.LockedUntil);
            Assert.NotNull(row.CompletedAt);
        }
    }
}
