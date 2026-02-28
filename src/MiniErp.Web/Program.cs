using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using MiniErp.Web.Components;
using MiniErp.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddScoped<ProtectedLocalStorage>();
builder.Services.AddScoped<ApiSession>();
builder.Services.AddHttpClient<ApiClient>((sp, http) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["Api:BaseUrl"] ?? "https://localhost:7002";
    http.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
});

var app = builder.Build();

var supportedCultures = new[] { new CultureInfo("ar"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("ar"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider()
    }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/ui/culture/{culture}", (HttpContext httpContext, string culture, string? redirect) =>
{
    if (string.IsNullOrWhiteSpace(culture) || !supportedCultures.Any(x => string.Equals(x.Name, culture, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.BadRequest(new { error = "INVALID_CULTURE" });
    }

    var value = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
    httpContext.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        value,
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

    var target = string.IsNullOrWhiteSpace(redirect) ? "/" : redirect;
    return Results.LocalRedirect(target);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
