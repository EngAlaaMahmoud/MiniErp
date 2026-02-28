using System.Diagnostics;

namespace MiniErp.Api.Infrastructure.Observability;

public sealed class RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger)
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Response.Headers[CorrelationHeader] = correlationId;

        var tenantId = context.User.FindFirst("tenant_id")?.Value
                       ?? (context.Request.Headers.TryGetValue("X-Tenant-Id", out var t) ? t.ToString() : null);
        var deviceId = context.User.FindFirst("device_id")?.Value
                       ?? (context.Request.Headers.TryGetValue("X-Device-Id", out var d) ? d.ToString() : null);

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlationId"] = correlationId,
            ["traceId"] = traceId,
            ["tenantId"] = tenantId,
            ["deviceId"] = deviceId
        });

        var start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var status = context.Response.StatusCode;
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0} ms",
                context.Request.Method,
                context.Request.Path.Value,
                status,
                elapsedMs);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var existing))
        {
            var value = existing.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= 200)
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}

