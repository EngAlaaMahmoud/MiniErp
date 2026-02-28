using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Data;
using MiniErp.Api.Infrastructure.Tenancy;

namespace MiniErp.Api.IntegrationTests;

public sealed class MiniErpApiFixture : IAsyncLifetime
{
    private MiniErpApiFactory? _factory;
    private string? _dbName;

    public HttpClient Client { get; private set; } = null!;
    public string ConnectionString { get; private set; } = "";
    public bool IsAvailable { get; private set; }
    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        _dbName = $"MiniErp_Test_{Guid.NewGuid():N}";

        var fromEnv = Environment.GetEnvironmentVariable("MINIERP_TEST_SQLSERVER");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            ConnectionString = fromEnv;
        }
        else
        {
            ConnectionString = $"Server=(localdb)\\\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
        }

        try
        {
            _factory = new MiniErpApiFactory(ConnectionString);
            Client = _factory.CreateClient();

            await using var db = CreateDbContext();
            await db.Database.MigrateAsync();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            await DisposeAsync();
            IsAvailable = false;
            UnavailableReason = $"SQL Server test database is not available. Set MINIERP_TEST_SQLSERVER to a reachable SQL Server connection string. {ex.GetType().Name}: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (Client is not null)
        {
            Client.Dispose();
        }
        _factory?.Dispose();

        if (_dbName is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINIERP_TEST_SQLSERVER")))
        {
            return;
        }

        try
        {
            var masterConn = "Server=(localdb)\\\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";
            await using var conn = new SqlConnection(masterConn);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"""
                 IF DB_ID(N'{_dbName}') IS NOT NULL
                 BEGIN
                   ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                   DROP DATABASE [{_dbName}];
                 END
                 """;
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup. Some environments won't have LocalDB.
        }
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options, new FixedTenantProvider(TestConstants.TenantId));
    }

    private sealed class FixedTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId => tenantId;
    }
}
