using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
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
        Func<CancellationToken, Task<(int StatusCode, TResponse Body)>> action,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var existing = await db.IdempotencyKeys
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Key == key, ct);

        if (existing is not null)
        {
            if (!string.Equals(existing.Endpoint, endpoint, StringComparison.Ordinal) ||
                !string.Equals(existing.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
            {
                return new ConflictObjectResult(new
                {
                    error = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST"
                });
            }

            if (existing.CompletedAt is null || existing.ResponseStatusCode is null || existing.ResponseBody is null)
            {
                return new ConflictObjectResult(new { error = "IDEMPOTENCY_REQUEST_IN_PROGRESS" });
            }

            await tx.CommitAsync(ct);
            return new ContentResult
            {
                StatusCode = existing.ResponseStatusCode,
                ContentType = "application/json",
                Content = existing.ResponseBody
            };
        }

        var row = new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviceId = deviceId,
            Key = key,
            Endpoint = endpoint,
            RequestHash = requestHash,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.IdempotencyKeys.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogWarning(ex, "Idempotency key unique violation; reloading existing row.");
            existing = await db.IdempotencyKeys
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Key == key, ct);

            if (existing is null)
            {
                throw;
            }

            if (existing.CompletedAt is null || existing.ResponseStatusCode is null || existing.ResponseBody is null)
            {
                return new ConflictObjectResult(new { error = "IDEMPOTENCY_REQUEST_IN_PROGRESS" });
            }

            await tx.CommitAsync(ct);
            return new ContentResult
            {
                StatusCode = existing.ResponseStatusCode,
                ContentType = "application/json",
                Content = existing.ResponseBody
            };
        }

        var (statusCode, body) = await action(ct);

        row.CompletedAt = DateTimeOffset.UtcNow;
        row.ResponseStatusCode = statusCode;
        row.ResponseBody = JsonSerializer.Serialize(body, JsonOptions);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new ContentResult
        {
            StatusCode = statusCode,
            ContentType = "application/json",
            Content = row.ResponseBody
        };
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return true;
        }

        return ex.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
