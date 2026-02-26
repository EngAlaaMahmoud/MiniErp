using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
