using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace MiniErp.Web.Services;

public sealed class ApiSession(ProtectedLocalStorage storage)
{
    private const string StorageKey = "miniErp.session.v2";
    private bool _loaded;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public event Action? Changed;

    public Guid TenantId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid BranchId { get; private set; }
    public string? AccessToken { get; private set; }
    public string? UserName { get; private set; }
    public string? Role { get; private set; }
    public IReadOnlyList<string> PermissionKeys { get; private set; } = Array.Empty<string>();

    public bool IsLoggedIn => TenantId != Guid.Empty && DeviceId != Guid.Empty && !string.IsNullOrWhiteSpace(AccessToken);

    public bool HasPermission(string key)
        => PermissionKeys.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        var changed = false;
        await _loadLock.WaitAsync();
        try
        {
            if (_loaded)
            {
                return;
            }

            ProtectedBrowserStorageResult<StoredSession> result;
            try
            {
                result = await storage.GetAsync<StoredSession>(StorageKey);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            _loaded = true;
            if (!result.Success || result.Value is null)
            {
                changed = true;
                return;
            }

            TenantId = result.Value.TenantId;
            DeviceId = result.Value.DeviceId;
            BranchId = result.Value.BranchId;
            AccessToken = result.Value.AccessToken;
            UserName = result.Value.UserName;
            Role = result.Value.Role;
            PermissionKeys = result.Value.PermissionKeys ?? Array.Empty<string>();
            changed = true;
        }
        finally
        {
            _loadLock.Release();
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public async Task SetAsync(Guid tenantId, Guid deviceId, Guid branchId, string accessToken, string userName, string role)
    {
        TenantId = tenantId;
        DeviceId = deviceId;
        BranchId = branchId;
        AccessToken = accessToken;
        UserName = userName;
        Role = role;
        await SaveAsync();
        Changed?.Invoke();
    }

    public async Task SetPermissionsAsync(IReadOnlyList<string> permissionKeys)
    {
        PermissionKeys = (permissionKeys ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await SaveAsync();
        Changed?.Invoke();
    }

    public async Task SaveAsync()
    {
        var session = new StoredSession(TenantId, DeviceId, BranchId, AccessToken, UserName, Role, PermissionKeys.ToArray());
        await storage.SetAsync(StorageKey, session);
    }

    public async Task ClearAsync()
    {
        TenantId = Guid.Empty;
        DeviceId = Guid.Empty;
        BranchId = Guid.Empty;
        AccessToken = null;
        UserName = null;
        Role = null;
        PermissionKeys = Array.Empty<string>();
        await storage.DeleteAsync(StorageKey);
        Changed?.Invoke();
    }

    private sealed record StoredSession(
        Guid TenantId,
        Guid DeviceId,
        Guid BranchId,
        string? AccessToken,
        string? UserName,
        string? Role,
        string[]? PermissionKeys
    );
}
