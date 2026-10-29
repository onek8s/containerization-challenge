using FurnaceRecords.Data;
using FurnaceRecords.Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;

var builder = WebApplication.CreateBuilder(args);

var portal = builder.Configuration.GetSection(PortalOptions.Section).Get<PortalOptions>() ?? new PortalOptions();
builder.Services.AddSingleton(portal);
builder.Services.AddFurnaceRecordsData(builder.Configuration);

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
    o.AddPolicy(Policies.Operator, p => p.RequireRole(portal.OperatorsGroup, portal.QualityGroup, portal.AdminsGroup));
    o.AddPolicy(Policies.Quality, p => p.RequireRole(portal.QualityGroup));
    o.AddPolicy(Policies.Admin, p => p.RequireRole(portal.AdminsGroup));
    o.FallbackPolicy = o.GetPolicy(Policies.Operator);
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Recipes", Policies.Admin);
    o.Conventions.AuthorizeFolder("/Furnaces", Policies.Admin);
});
builder.Services.AddHttpClient<DaqClient>(c =>
{
    c.BaseAddress = new Uri(portal.DaqAddress.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<FurnaceRecordsContext>(),
        scope.ServiceProvider.GetRequiredService<DatabaseOptions>());
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
