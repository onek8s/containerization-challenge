using System.Security.Cryptography.X509Certificates;
using Humongous.Identity.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

var connectionString = builder.Configuration.GetConnectionString("Identity")
    ?? throw new InvalidOperationException("Connection string 'Identity' is missing.");

builder.Services.AddDbContext<IdentityContext>(options =>
{
    if (builder.Configuration["Database:Provider"] == "Sqlite")
        options.UseSqlite(connectionString);
    else
        options.UseSqlServer(connectionString);
    options.UseOpenIddict();
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
        options.ClaimsIdentity.UserNameClaimType = Claims.Name;
        options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
        options.ClaimsIdentity.RoleClaimType = Claims.Role;
    })
    .AddEntityFrameworkStores<IdentityContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "hq.login";
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

var identitySection = builder.Configuration.GetSection("Identity");
builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<IdentityContext>())
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(identitySection["Issuer"]!));
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token")
            .SetLogoutEndpointUris("connect/logout");
        options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();
        options.RegisterScopes(Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, CustomClaims.Scope);
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(identitySection.GetValue("AccessTokenMinutes", 30)));
        options.SetRefreshTokenLifetime(TimeSpan.FromHours(identitySection.GetValue("RefreshTokenHours", 10)));

        // The quote API validates plain signed JWTs with the standard JWT bearer handler.
        options.DisableAccessTokenEncryption();

        var signing = identitySection["SigningCertificateThumbprint"];
        var encryption = identitySection["EncryptionCertificateThumbprint"];
        if (!string.IsNullOrEmpty(signing) && !string.IsNullOrEmpty(encryption))
        {
            options.AddSigningCertificate(signing, StoreName.My, StoreLocation.LocalMachine);
            options.AddEncryptionCertificate(encryption, StoreName.My, StoreLocation.LocalMachine);
        }
        else
        {
            options.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
        }

        var aspNetCore = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableLogoutEndpointPassthrough();
        if (!identitySection.GetValue("RequireHttps", true))
            aspNetCore.DisableTransportSecurityRequirement();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "UserAdmin");
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UserAdmin", p => p.RequireRole(Roles.Administrator, Roles.BrokerAdmin));
});
builder.Services.AddHostedService<IdentitySeeder>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
