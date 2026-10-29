using FreightRates.Admin.Services;
using FreightRates.Contracts;
using FreightRates.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.Section));
var admin = builder.Configuration.GetSection(AdminOptions.Section).Get<AdminOptions>() ?? new AdminOptions();

if (string.Equals(admin.Authentication, DevelopmentAuthenticationHandler.SchemeName, StringComparison.OrdinalIgnoreCase))
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
        .RequireRole(admin.AdminGroup)
        .Build();
});

builder.Services.AddFreightRatesData(builder.Configuration);
builder.Services.AddRatingHostClient(builder.Configuration);
builder.Services.AddScoped<TariffUploadService>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = admin.MaxTariffUploadBytes);

builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FreightRatesContext>();
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
