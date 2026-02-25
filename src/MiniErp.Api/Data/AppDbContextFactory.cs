using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MiniErp.Api.Infrastructure.Tenancy;

namespace MiniErp.Api.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var sqlServer = configuration.GetConnectionString("SqlServerLocalDb") ?? configuration.GetConnectionString("SqlServer");
        var postgres = configuration.GetConnectionString("Postgres");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        if (!string.IsNullOrWhiteSpace(sqlServer))
        {
            optionsBuilder.UseSqlServer(sqlServer);
        }
        else if (!string.IsNullOrWhiteSpace(postgres))
        {
            optionsBuilder.UseNpgsql(postgres);
        }
        else
        {
            throw new InvalidOperationException("Missing connection string for design-time DbContext.");
        }

        return new AppDbContext(optionsBuilder.Options, new FixedTenantProvider(Guid.Empty));
    }

    private sealed class FixedTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; } = tenantId;
    }
}
