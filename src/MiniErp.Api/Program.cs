using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<MiniErp.Api.Infrastructure.Tenancy.ITenantProvider, MiniErp.Api.Infrastructure.Tenancy.HttpHeaderTenantProvider>();
builder.Services.AddDbContext<MiniErp.Api.Data.AppDbContext>((sp, options) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var sqlServer = configuration.GetConnectionString("SqlServerLocalDb") ?? configuration.GetConnectionString("SqlServer");
    if (!string.IsNullOrWhiteSpace(sqlServer))
    {
        options.UseSqlServer(sqlServer);
        return;
    }

    var postgres = configuration.GetConnectionString("Postgres");
    if (!string.IsNullOrWhiteSpace(postgres))
    {
        options.UseNpgsql(postgres);
        return;
    }

    throw new InvalidOperationException("Missing connection string. Set ConnectionStrings:SqlServerLocalDb (recommended) or ConnectionStrings:Postgres.");
});
builder.Services.AddScoped<MiniErp.Api.Services.IdempotencyService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
