using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NLog.Web;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Portal.Hubs;
using Proseware.ControlTower.Portal.Infrastructure;
using Proseware.ControlTower.Portal.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
if (builder.Environment.IsDevelopment())
    NLog.LogManager.Configuration.Variables["logDir"] = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../.devdata/logs"));

var configuration = builder.Configuration;
builder.Services.AddControlTowerData(configuration);
builder.Services.AddControlTowerMessaging(configuration);
builder.Services.AddControlTowerDocumentStore(configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CustomerContext>();
builder.Services.AddScoped<PortalShipments>();
builder.Services.AddScoped<BookingRequestService>();
builder.Services.AddHostedService<LiveUpdatesService>();

builder.Services.AddDataProtection()
    .SetApplicationName("ControlTower")
    .PersistKeysToFileSystem(new DirectoryInfo(configuration["DataProtection:KeyPath"] ?? @"D:\ControlTower\Keys\track"));

if (builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(configuration["Authentication:DevelopmentUser"]))
{
    builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.Scheme)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.Scheme, _ => { });
}
else
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name = ".ControlTower.Portal";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.AccessDeniedPath = "/NoAccount";
        })
        .AddOpenIdConnect(options =>
        {
            options.Authority = configuration["Authentication:Authority"];
            options.RequireHttpsMetadata = configuration.GetValue("Authentication:RequireHttpsMetadata", true);
            options.ClientId = configuration["Authentication:ClientId"] ?? "portal";
            options.ClientSecret = configuration["Authentication:ClientSecret"];
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.Scope.Clear();
            foreach (var scope in new[] { "openid", "profile", "email", "roles", "customer" }) options.Scope.Add(scope);
            options.ClaimActions.MapJsonKey("role", "role");
            options.ClaimActions.MapJsonKey(CustomerContext.CustomerIdClaim, CustomerContext.CustomerIdClaim);
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = "role";
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", p => p.RequireAuthenticatedUser().RequireClaim(CustomerContext.CustomerIdClaim));
    options.FallbackPolicy = options.GetPolicy("Customer");
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Track");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AllowAnonymousToPage("/NoAccount");
    options.Conventions.AuthorizePage("/SignOut", "Customer");
});
builder.Services.AddSignalR();
builder.Services.AddHealthChecks().AddDbContextCheck<ControlTowerDbContext>("database");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.EnsureDevelopmentDatabaseAsync(seed: true);
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<TrackingHub>("/hubs/tracking");
// Short link printed as QR code on the transport documents.
app.MapGet("/t/{number}", (string number) => Results.Redirect("/track?number=" + Uri.EscapeDataString(number))).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
