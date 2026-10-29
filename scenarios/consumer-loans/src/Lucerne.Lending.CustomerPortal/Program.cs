using Lucerne.Lending.CustomerPortal.Identity;
using Lucerne.Lending.CustomerPortal.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();

var identityConnection = builder.Configuration.GetConnectionString("PortalIdentity");
builder.Services.AddDbContext<PortalIdentityDbContext>(o =>
{
    if (builder.Configuration.GetValue("Database:Provider", "SqlServer") == "Sqlite") o.UseSqlite(identityConnection);
    else o.UseSqlServer(identityConnection);
});
builder.Services.AddIdentity<PortalUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedAccount = false;
        o.Password.RequiredLength = 10;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<PortalIdentityDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<PortalClaimsFactory>();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "LucerneApply";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.LoginPath = "/account/login";
    o.LogoutPath = "/account/logout";
    o.AccessDeniedPath = "/account/manage/two-factor";
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/portal-api")) ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/portal-api")) ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});
var keysFolder = builder.Configuration["DataProtection:KeysFolder"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Lucerne.Lending.CustomerPortal");
if (!string.IsNullOrEmpty(keysFolder)) dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysFolder));

var portal = builder.Configuration.GetSection("Portal").Get<PortalOptions>() ?? new PortalOptions();
builder.Services.AddSingleton(portal);
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Customer", p =>
    {
        p.RequireAuthenticatedUser().RequireClaim(PortalClaims.PersonalIdentityCode);
        // strong authentication: the online service requires an authenticator app once enrolled
        if (portal.RequireTwoFactor) p.RequireClaim("amr", "mfa");
    });
});

var lendingOptions = builder.Configuration.GetSection("LendingApi").Get<LendingApiOptions>() ?? new LendingApiOptions();
builder.Services.AddHttpClient<LendingApiClient>(http => LendingApiClient.Configure(http, lendingOptions));
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Account/Manage");
});
builder.Services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();
if (builder.Configuration.GetValue("Database:Upgrade", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PortalIdentityDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DevelopmentUsers.SeedAsync(scope.ServiceProvider, builder.Configuration.GetSection("Seed:Users"), app.Logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/account/error");
    app.UseHsts();
}
app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PortalRequestHeaderMiddleware>();

app.MapRazorPages();
app.MapPortalApi();
app.MapGet("/portal-api/config", (PortalOptions o) => Results.Ok(new { o.BankName, o.SupportPhone, o.RequireTwoFactor })).AllowAnonymous();
// the customer client is a single page application with history routing
app.MapFallbackToFile("app/index.html").RequireAuthorization("Customer");
app.Run();

public sealed class PortalOptions
{
    public string BankName { get; set; } = "Lucerne Savings Bank";
    public string SupportPhone { get; set; } = "+358 20 790 1100";
    public bool RequireTwoFactor { get; set; } = true;
}
