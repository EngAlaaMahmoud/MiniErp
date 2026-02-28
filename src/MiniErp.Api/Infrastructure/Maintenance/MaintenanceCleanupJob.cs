using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Data;
using MiniErp.Api.Domain.Enums;

namespace MiniErp.Api.Infrastructure.Maintenance;

public sealed class MaintenanceCleanupJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<MaintenanceCleanupJob> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var runEveryMinutes = configuration.GetValue("Maintenance:CleanupJob:RunEveryMinutes", 30);
            if (runEveryMinutes < 1)
            {
                runEveryMinutes = 30;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var now = DateTimeOffset.UtcNow;

                var retentionDays = configuration.GetValue("Maintenance:IdempotencyRetentionDays", 14);
                if (retentionDays < 1)
                {
                    retentionDays = 14;
                }

                var staleInProgressHours = configuration.GetValue("Maintenance:IdempotencyStaleInProgressHours", 12);
                if (staleInProgressHours < 1)
                {
                    staleInProgressHours = 12;
                }

                var cutoff = now.AddDays(-retentionDays);

                var deletedIdempotency = await db.IdempotencyKeys
                    .IgnoreQueryFilters()
                    .Where(x => (x.Status == IdempotencyStatus.Completed || x.Status == IdempotencyStatus.Failed) && x.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                var staleCutoff = now.AddHours(-staleInProgressHours);
                var staleBody = JsonSerializer.Serialize(new { error = "IDEMPOTENCY_STALE" }, JsonOptions);

                var staleMarked = await db.IdempotencyKeys
                    .IgnoreQueryFilters()
                    .Where(x => x.Status == IdempotencyStatus.InProgress && x.CreatedAt < staleCutoff)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, IdempotencyStatus.Failed)
                        .SetProperty(x => x.CompletedAt, now)
                        .SetProperty(x => x.LockedUntil, (DateTimeOffset?)null)
                        .SetProperty(x => x.ResponseStatusCode, StatusCodes.Status409Conflict)
                        .SetProperty(x => x.ResponseBody, staleBody)
                        .SetProperty(x => x.LastError, "Marked stale by maintenance cleanup job"),
                        stoppingToken);

                var printRetentionDays = configuration.GetValue("Maintenance:PrintJobRetentionDays", 14);
                if (printRetentionDays < 1)
                {
                    printRetentionDays = 14;
                }

                var printCutoff = now.AddDays(-printRetentionDays);
                var deletedPrintJobs = await db.PrintJobs
                    .IgnoreQueryFilters()
                    .Where(x => (x.Status == PrintJobStatus.Done || x.Status == PrintJobStatus.Failed) && x.CreatedAt < printCutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                logger.LogInformation(
                    "Maintenance cleanup done. idempotencyDeleted={IdempotencyDeleted} idempotencyStaleMarked={IdempotencyStaleMarked} printJobsDeleted={PrintJobsDeleted}",
                    deletedIdempotency,
                    staleMarked,
                    deletedPrintJobs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Maintenance cleanup job failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(runEveryMinutes), stoppingToken);
        }
    }
}

