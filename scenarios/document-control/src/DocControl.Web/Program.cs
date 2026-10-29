using Litware.DocControl.Data;
using Litware.DocControl.Web.Security;
using Litware.DocControl.Web.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDocControlData(builder.Configuration);
builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection(PortalOptions.SectionName));
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<RevisionUploadService>();
builder.Services.AddHttpContextAccessor();

var roles = builder.Configuration.GetSection(PortalOptions.SectionName).Get<PortalOptions>() ?? new PortalOptions();

if (string.Equals(builder.Configuration["Authentication:Mode"], "Development", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
        .AddScheme<DevelopmentAuthenticationOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName,
            o => builder.Configuration.GetSection("Authentication:Development").Bind(o));
}
else
{
    builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
}

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.Engineers, p => p.RequireRole(roles.EngineersGroup, roles.ControllersGroup));
    o.AddPolicy(Policies.Controllers, p => p.RequireRole(roles.ControllersGroup));
    o.FallbackPolicy = o.GetPolicy(Policies.Engineers);
});

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = roles.MaxUploadBytes);
builder.Services.AddControllersWithViews();

if (builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<SampleFileSeeder>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
