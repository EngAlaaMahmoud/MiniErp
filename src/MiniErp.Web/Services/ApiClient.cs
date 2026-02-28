using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MiniErp.Web.Services;

public sealed class ApiClient(HttpClient http, ApiSession session)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HttpRequestOptionsKey<bool> WasAuthenticatedKey = new("MiniErp.WasAuthenticated");

    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, path, ct);
        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            request.Options.TryGetValue(WasAuthenticatedKey, out var wasAuthenticated);
            throw new ApiUnauthorizedException(wasAuthenticated);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new ApiHttpException(response.StatusCode, body);
        }

        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    public async Task<PinLoginResult> PinLoginAsync(Guid tenantId, Guid deviceId, string userName, string pin, CancellationToken ct = default)
    {
        var body = new { userName, pin };
        var json = JsonSerializer.Serialize(body, Json);

        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/pin");
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString());
        request.Headers.TryAddWithoutValidation("X-Device-Id", deviceId.ToString());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return new PinLoginResult(false, null, response.StatusCode, responseBody);
        }

        return new PinLoginResult(true, responseBody, response.StatusCode, null);
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string path, T body, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(body, Json);
        using var request = await CreateRequestAsync(HttpMethod.Post, path, ct);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await http.SendAsync(request, ct);
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string path, T body, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(body, Json);
        using var request = await CreateRequestAsync(HttpMethod.Put, path, ct);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await http.SendAsync(request, ct);
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string path, T body, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(body, Json);
        using var request = await CreateRequestAsync(HttpMethod.Post, path, ct);
        foreach (var kvp in headers)
        {
            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return await http.SendAsync(request, ct);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken ct)
    {
        await session.EnsureLoadedAsync();
        var request = new HttpRequestMessage(method, path.TrimStart('/'));
        request.Options.Set(WasAuthenticatedKey, !string.IsNullOrWhiteSpace(session.AccessToken));

        if (session.TenantId != Guid.Empty)
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", session.TenantId.ToString());
        }

        if (session.DeviceId != Guid.Empty)
        {
            request.Headers.TryAddWithoutValidation("X-Device-Id", session.DeviceId.ToString());
        }

        if (!string.IsNullOrWhiteSpace(session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        return request;
    }
}

public sealed record PinLoginResult(
    bool Success,
    string? JsonBody,
    System.Net.HttpStatusCode StatusCode,
    string? ErrorBody
);

public sealed class ApiUnauthorizedException : Exception
{
    public ApiUnauthorizedException(bool wasAuthenticated) : base("Unauthorized")
    {
        WasAuthenticated = wasAuthenticated;
    }

    public bool WasAuthenticated { get; }
}

public sealed class ApiHttpException(System.Net.HttpStatusCode statusCode, string body) : Exception($"HTTP {(int)statusCode} {statusCode}")
{
    public System.Net.HttpStatusCode StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}
