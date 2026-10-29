using Microsoft.AspNetCore.Server.IISIntegration;
using StaffOnboarding.Portal;
using StaffOnboarding.Portal.Data;
using StaffOnboarding.Portal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection(PortalOptions.Section));
var options = builder.Configuration.GetSection(PortalOptions.Section).Get<PortalOptions>() ?? new PortalOptions();

// Windows authentication is done by IIS; roles are the user's AD groups.
builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);
builder.Services.AddAuthorization(a =>
{
    a.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    a.AddPolicy("HR", p => p.RequireRole($"{options.Domain}\\{options.HrGroup}"));
    a.AddPolicy("HRorIT", p => p.RequireRole($"{options.Domain}\\{options.HrGroup}", $"{options.Domain}\\{options.ItGroup}"));
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Requests", "HRorIT");
    o.Conventions.AuthorizePage("/Requests/NewJoiner", "HR");
    o.Conventions.AuthorizePage("/Requests/NewMover", "HR");
    o.Conventions.AuthorizePage("/Requests/NewLeaver", "HR");
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<OnboardingRepository>();
builder.Services.AddSingleton<WorkflowGateway>();
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddSingleton<ReferenceData>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.MapGet("/requests/{number}/documents/{file}", (string number, string file, DocumentStore documents) =>
    documents.Find(number, file) is { } f
        ? Results.File(f.FullName, "application/octet-stream", f.Name)
        : Results.NotFound()).RequireAuthorization("HRorIT");

app.Run();
