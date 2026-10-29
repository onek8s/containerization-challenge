using Lucerne.Lending.BackOffice.Infrastructure;
using Lucerne.Lending.Data;
using Lucerne.Lending.Workflow;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.WsFederation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddLendingWorkflow(builder.Configuration);
var auth = builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = builder.Configuration.GetValue("Auth:Mode", "WsFederation") == "Development"
        ? CookieAuthenticationDefaults.AuthenticationScheme
        : WsFederationDefaults.AuthenticationScheme;
}).AddCookie(o =>
{
    o.Cookie.Name = "LucerneLendingOffice";
    o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/account/denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(9);
    o.SlidingExpiration = true;
});
if (builder.Configuration.GetValue("Auth:Mode", "WsFederation") != "Development")
{
    auth.AddWsFederation(o =>
    {
        // ADFS 2019, relying party trust https://lending-office.corp.local/
        o.MetadataAddress = builder.Configuration["Auth:MetadataAddress"];
        o.Wtrealm = builder.Configuration["Auth:Realm"];
        o.TokenValidationParameters.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
        o.UseTokenLifetime = false;
    });
}
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireRole(Roles.All).Build();
    o.AddPolicy("Underwriting", p => p.RequireRole(Roles.Underwriter, Roles.SeniorUnderwriter));
    o.AddPolicy("Operations", p => p.RequireRole(Roles.Operations));
    o.AddPolicy("Collections", p => p.RequireRole(Roles.Collections));
});
builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();
if (builder.Configuration.GetValue("Database:Upgrade", false))
{
    SchemaUpgrader.Upgrade(app.Services.GetRequiredService<LendingDbOptions>(), app.Logger);
    await ReferenceData.SeedAsync(app.Services.GetRequiredService<ProductRepository>());
}
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/home/error");
app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
