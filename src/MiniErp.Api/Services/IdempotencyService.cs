using System.Security.Cryptography;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using Npgsql;

namespace MiniErp.Api.Services;

public sealed class IdempotencyService(ILogger<IdempotencyService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string HashRequest<T>(T request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    public async Task<IActionResult> ExecuteAsync<TResponse>(
        AppDbContext db,
        Guid tenantId,
        Guid deviceId,
        string endpoint,
        string key,
        string requestHash,
        Func<Guid, CancellationToken, Task<(int StatusCode, TResponse Body)>> action,
        TimeSpan inProgressTtl,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var claim = await ClaimAsync(db, tenantId, deviceId, endpoint, key, requestHash, now, inProgressTtl, ct);
        if (claim.Result is not null)
        {
            return claim.Result;
        }

        var operationId = claim.RowId!.Value;

        try
        {
            var (statusCode, body) = await action(operationId, ct);
            await CompleteAsync(db, operationId, tenantId, statusCode, body, ct);

            return new ContentResult
            {
                StatusCode = statusCode,
                ContentType = "application/json",
                Content = JsonSerializer.Serialize(body, JsonOptions)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Idempotent action failed. endpoint={Endpoint} key={Key}", endpoint, key);
            await FailAsync(db, operationId, tenantId, ex, ct);
            throw;
        }
    }

    private async Task<(Guid? RowId, IActionResult? Result)> ClaimAsync(
        AppDbContext db,
        Guid tenantId,
        Guid deviceId,
        string endpoint,
        string key,
        string requestHash,
        DateTimeOffset now,
        TimeSpan ttl,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var existing = await db.IdempotencyKeys
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Key == key, ct);

            if (existing is null)
            {
                var row = new IdempotencyKey
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DeviceId = deviceId,
                    Key = key,
                    Endpoint = endpoint,
                    RequestHash = requestHash,
                    CreatedAt = now,
                    Status = IdempotencyStatus.InProgress,
                    LockedUntil = now.Add(ttl),
                    AttemptCount = 1
                };

                db.IdempotencyKeys.Add(row);
                try
                {
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return (row.Id, null);
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    logger.LogWarning(ex, "Idempotency key unique violation; reloading existing row.");
                    await tx.RollbackAsync(ct);
                    db.ChangeTracker.Clear();
                    continue;
                }
            }

            if (!string.Equals(existing.Endpoint, endpoint, StringComparison.Ordinal) ||
                !string.Equals(existing.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
            {
                return (null, new ConflictObjectResult(new { error = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST" }));
            }

            if (existing.Status == IdempotencyStatus.Completed &&
                existing.ResponseStatusCode is not null &&
                existing.ResponseBody is not null)
            {
                await tx.CommitAsync(ct);
                return (null, new ContentResult
                {
                    StatusCode = existing.ResponseStatusCode,
                    ContentType = "application/json",
                    Content = existing.ResponseBody
                });
            }

            if (existing.Status == IdempotencyStatus.InProgress)
            {
                var lockUntil = existing.LockedUntil ?? existing.CreatedAt.Add(ttl);
                if (lockUntil > now)
                {
                    return (null, new ConflictObjectResult(new { error = "IDEMPOTENCY_REQUEST_IN_PROGRESS" }));
                }

                var affected = await db.IdempotencyKeys
                    .IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Key == key && x.Status == IdempotencyStatus.InProgress && (x.LockedUntil == null || x.LockedUntil <= now))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.LockedUntil, now.Add(ttl))
                        .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                        .SetProperty(x => x.LastError, (string?)null),
                        ct);

                if (affected == 1)
                {
                    await tx.CommitAsync(ct);
                    return (existing.Id, null);
                }

                var refreshed = await db.IdempotencyKeys
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .SingleAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Key == key, ct);

                if (refreshed.Status == IdempotencyStatus.Completed &&
                    refreshed.ResponseStatusCode is not null &&
                    refreshed.ResponseBody is not null)
                {
                    await tx.CommitAsync(ct);
                    return (null, new ContentResult
                    {
                        StatusCode = refreshed.ResponseStatusCode,
                        ContentType = "application/json",
                        Content = refreshed.ResponseBody
                    });
                }

                return (null, new ConflictObjectResult(new { error = "IDEMPOTENCY_REQUEST_IN_PROGRESS" }));
            }

            var retryAffected = await db.IdempotencyKeys
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Key == key && x.Status == IdempotencyStatus.Failed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, IdempotencyStatus.InProgress)
                    .SetProperty(x => x.LockedUntil, now.Add(ttl))
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                    .SetProperty(x => x.LastError, (string?)null)
                    .SetProperty(x => x.CompletedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.ResponseStatusCode, (int?)null)
                    .SetProperty(x => x.ResponseBody, (string?)null),
                    ct);

            if (retryAffected == 1)
            {
                await tx.CommitAsync(ct);
                return (existing.Id, null);
            }

            var retryRefreshed = await db.IdempotencyKeys
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Key == key, ct);

            if (retryRefreshed.Status == IdempotencyStatus.Completed &&
                retryRefreshed.ResponseStatusCode is not null &&
                retryRefreshed.ResponseBody is not null)
            {
                await tx.CommitAsync(ct);
                return (null, new ContentResult
                {
                    StatusCode = retryRefreshed.ResponseStatusCode,
                    ContentType = "application/json",
                    Content = retryRefreshed.ResponseBody
                });
            }

            return (null, new ConflictObjectResult(new { error = "IDEMPOTENCY_REQUEST_IN_PROGRESS" }));
        }

        throw new InvalidOperationException("Failed to claim idempotency key.");
    }

    private static async Task CompleteAsync<TResponse>(
        AppDbContext db,
        Guid rowId,
        Guid tenantId,
        int statusCode,
        TResponse body,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.IdempotencyKeys
            .IgnoreQueryFilters()
            .SingleAsync(x => x.TenantId == tenantId && x.Id == rowId, ct);

        row.Status = IdempotencyStatus.Completed;
        row.CompletedAt = now;
        row.LockedUntil = null;
        row.ResponseStatusCode = statusCode;
        row.ResponseBody = JsonSerializer.Serialize(body, JsonOptions);
        row.LastError = null;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task FailAsync(
        AppDbContext db,
        Guid rowId,
        Guid tenantId,
        Exception ex,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.IdempotencyKeys
            .IgnoreQueryFilters()
            .SingleAsync(x => x.TenantId == tenantId && x.Id == rowId, ct);

        row.Status = IdempotencyStatus.Failed;
        row.CompletedAt = now;
        row.LockedUntil = null;
        row.ResponseStatusCode = StatusCodes.Status500InternalServerError;
        row.ResponseBody = JsonSerializer.Serialize(new { error = "INTERNAL_ERROR" }, JsonOptions);
        row.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return true;
        }

        if (ex.InnerException is DbException { ErrorCode: 19 } inner && inner.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ex.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
