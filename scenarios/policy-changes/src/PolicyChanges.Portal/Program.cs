using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using PolicyChanges.Data;
using PolicyChanges.Portal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection(PortalOptions.Section));
var portal = builder.Configuration.GetSection(PortalOptions.Section).Get<PortalOptions>() ?? new PortalOptions();

if (string.Equals(portal.Authentication, DevelopmentAuthenticationHandler.SchemeName, StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, null);
}
else
{
    builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
}

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(portal.UsersGroup, portal.LeadsGroup)
        .Build();
    o.AddPolicy(Policies.Leads, p => p.RequireRole(portal.LeadsGroup));
});

builder.Services.AddPolicyChangesData(builder.Configuration);
builder.Services.AddSingleton<EvidenceStore>();
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Partners", Policies.Leads);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PolicyChangesContext>();
    await DbInitializer.InitializeAsync(db, app.Configuration.GetValue<bool>("Database:SeedSampleData"));
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
