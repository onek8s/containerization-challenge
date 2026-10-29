using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NLog.Web;
using Proseware.ControlTower.Booking.Controllers;
using Proseware.ControlTower.Booking.Infrastructure;
using Proseware.ControlTower.Booking.Models;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Integration.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
if (builder.Environment.IsDevelopment())
    NLog.LogManager.Configuration.Variables["logDir"] = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../.devdata/logs"));

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container => container.RegisterModule<BookingModule>());

var configuration = builder.Configuration;
builder.Services.AddControlTowerData(configuration);
builder.Services.AddControlTowerMessaging(configuration);
builder.Services.AddControlTowerDocumentStore(configuration);
builder.Services.AddAutoMapper(typeof(MappingProfile));
builder.Services.AddHttpContextAccessor();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".ControlTower.Booking.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

builder.Services.AddDataProtection()
    .SetApplicationName("ControlTower")
    .PersistKeysToFileSystem(new DirectoryInfo(configuration["DataProtection:KeyPath"] ?? @"D:\ControlTower\Keys\booking"));

var useDevelopmentUser = builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(configuration["Authentication:DevelopmentUser"]);
if (useDevelopmentUser)
{
    builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.Scheme)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.Scheme, _ => { });
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
            options.Cookie.Name = ".ControlTower.Booking";
            options.ExpireTimeSpan = TimeSpan.FromHours(10);
            options.SlidingExpiration = true;
            options.AccessDeniedPath = "/Account/AccessDenied";
        })
        .AddOpenIdConnect(options =>
        {
            options.Authority = configuration["Authentication:Authority"];
            options.RequireHttpsMetadata = configuration.GetValue("Authentication:RequireHttpsMetadata", true);
            options.ClientId = configuration["Authentication:ClientId"] ?? "booking";
            options.ClientSecret = configuration["Authentication:ClientSecret"];
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.Scope.Clear();
            foreach (var scope in new[] { "openid", "profile", "email", "roles", "booking.api" }) options.Scope.Add(scope);
            options.ClaimActions.MapJsonKey("role", "role");
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = "role";
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.Staff, p => p.RequireRole("booking", "ops", "admin"));
    options.AddPolicy(Policies.Booking, p => p.RequireRole("booking", "admin"));
    options.AddPolicy(Policies.Admin, p => p.RequireRole("admin"));
});

builder.Services.AddControllersWithViews()
    .AddFluentValidation(fv =>
    {
        fv.RegisterValidatorsFromAssemblyContaining<CustomerEditValidator>();
        fv.AutomaticValidationEnabled = false;
    });
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
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
