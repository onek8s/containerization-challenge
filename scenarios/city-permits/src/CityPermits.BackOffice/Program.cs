using System.Security.Claims;
using CityPermits.Archive;
using CityPermits.BackOffice.Services;
using CityPermits.Data;
using CityPermits.Documents;
using CityPermits.Domain.Workflow;
using CityPermits.Gis;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.WsFederation;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
var cfg = builder.Configuration;

builder.Services.AddPermitsData(cfg);
builder.Services.AddCityGis(cfg);
builder.Services.AddDocumentArchive(cfg);
builder.Services.AddSingleton(new PermitDocuments(cfg["Documents:VerifyBaseUrl"] ?? "https://permits.ashford.gov.uk/verify/"));
builder.Services.AddScoped<CaseActions>();

if (cfg["Authentication:Mode"] == "Development")
{
    builder.Services.AddAuthentication(DevelopmentAuthHandler.Scheme)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevelopmentAuthHandler>(DevelopmentAuthHandler.Scheme, null);
}
else
{
    // ADFS 2019 relying party "City Permits back office"; group claims are mapped to roles by the
    // issuance transform rules (PRM-CaseOfficers -> CaseOfficer, PRM-Supervisors -> Supervisor, ...).
    builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = WsFederationDefaults.AuthenticationScheme;
    })
    .AddCookie(o => { o.ExpireTimeSpan = TimeSpan.FromHours(8); o.Cookie.Name = "ashford.permits.office"; })
    .AddWsFederation(o =>
    {
        o.MetadataAddress = cfg["Authentication:MetadataAddress"];
        o.Wtrealm = cfg["Authentication:Realm"];
        o.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
        o.TokenValidationParameters.NameClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/windowsaccountname";
    });
}
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireRole(Roles.CaseOfficer, Roles.Supervisor, Roles.Finance, Roles.Administrator).Build();
    o.AddPolicy("Supervisor", p => p.RequireRole(Roles.Supervisor));
    o.AddPolicy("Finance", p => p.RequireRole(Roles.Finance, Roles.Supervisor));
});
builder.Services.AddControllersWithViews();

var app = builder.Build();
app.UseSerilogRequestLogging();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Cases/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Cases}/{action=Index}/{id?}");
app.MapGet("/map/export/{id:int}", async (int id, CaseActions actions, IGisService gis) =>
{
    var c = await actions.LoadAsync(id);
    if (c?.ParcelId is null) return Results.NotFound();
    var img = gis.ExportMap(new CityPermits.Contracts.MapExportRequest("SiteLocation", c.ParcelId, c.Building?.FootprintWkt, c.Title, c.Reference));
    return Results.File(img.Content, img.ContentType);
});
app.Run();
