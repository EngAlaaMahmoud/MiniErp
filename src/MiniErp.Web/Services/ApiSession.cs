using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace MiniErp.Web.Services;

public sealed class ApiSession(ProtectedLocalStorage storage)
{
    private const string StorageKey = "miniErp.session.v1";
    private bool _loaded;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public Guid TenantId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid BranchId { get; private set; }
    public string? AccessToken { get; private set; }
    public string? UserName { get; private set; }
    public string? Role { get; private set; }

    public bool IsLoggedIn => TenantId != Guid.Empty && DeviceId != Guid.Empty && !string.IsNullOrWhiteSpace(AccessToken);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

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
                return;
            }

            TenantId = result.Value.TenantId;
            DeviceId = result.Value.DeviceId;
            BranchId = result.Value.BranchId;
            AccessToken = result.Value.AccessToken;
            UserName = result.Value.UserName;
            Role = result.Value.Role;
        }
        finally
        {
            _loadLock.Release();
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
    }

    public async Task SaveAsync()
    {
        var session = new StoredSession(TenantId, DeviceId, BranchId, AccessToken, UserName, Role);
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
        await storage.DeleteAsync(StorageKey);
    }

    private sealed record StoredSession(
        Guid TenantId,
        Guid DeviceId,
        Guid BranchId,
        string? AccessToken,
        string? UserName,
        string? Role
    );
}
