using System.Text;
using MiniErp.Api.Infrastructure.Observability;
using MiniErp.Api.Infrastructure.Maintenance;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        var traceId = System.Diagnostics.Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Extensions["traceId"] = traceId;
        if (ctx.HttpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId))
        {
            ctx.ProblemDetails.Extensions["correlationId"] = correlationId.ToString();
        }
    };
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Type = "https://httpstatuses.com/400",
            Title = "One or more validation errors occurred."
        };
        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

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
builder.Services.AddScoped<MiniErp.Api.Security.PermissionService>();
builder.Services.AddSingleton<MiniErp.Api.Services.PinHasher>();
builder.Services.Configure<MiniErp.Api.Services.JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<MiniErp.Api.Services.JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt").Get<MiniErp.Api.Services.JwtOptions>() ?? new MiniErp.Api.Services.JwtOptions();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
        };
    });
builder.Services.AddAuthorization();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<MiniErp.Api.Infrastructure.Dev.DevSeeder>();
}

if (builder.Configuration.GetValue("Maintenance:CleanupJob:Enabled", true))
{
    builder.Services.AddHostedService<MaintenanceCleanupJob>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<RequestTelemetryMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
