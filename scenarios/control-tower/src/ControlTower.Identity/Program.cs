using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NLog.Web;
using Proseware.ControlTower.Identity;
using Proseware.ControlTower.Identity.Data;
using Proseware.ControlTower.Identity.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
if (builder.Environment.IsDevelopment())
    NLog.LogManager.Configuration.Variables["logDir"] = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../.devdata/logs"));

var configuration = builder.Configuration;
var connectionString = configuration.GetConnectionString("Identity")
    ?? throw new InvalidOperationException("Connection string 'Identity' is missing.");
var useSqlite = string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

void UseDatabase(DbContextOptionsBuilder options)
{
    if (useSqlite)
        options.UseSqlite(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
    else
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(migrationsAssembly);
            sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        });
}

builder.Services.AddDbContext<AuthDbContext>(UseDatabase);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();

var isConfig = configuration.GetSection("IdentityServer");
var identityServer = builder.Services
    .AddIdentityServer(options =>
    {
        options.IssuerUri = isConfig["IssuerUri"];
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;
        options.UserInteraction.LoginUrl = "/Account/Login";
        options.UserInteraction.LogoutUrl = "/Account/Logout";
        options.UserInteraction.ErrorUrl = "/Error";
        options.Authentication.CookieLifetime = TimeSpan.FromHours(10);
        options.Authentication.CookieSlidingExpiration = true;
    })
    .AddInMemoryIdentityResources(IdentityResourcesConfig.All)
    .AddInMemoryApiScopes(isConfig.GetSection("ApiScopes"))
    .AddInMemoryApiResources(isConfig.GetSection("ApiResources"))
    .AddInMemoryClients(isConfig.GetSection("Clients"))
    .AddAspNetIdentity<ApplicationUser>()
    .AddProfileService<ControlTowerProfileService>()
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = UseDatabase;
        options.DefaultSchema = useSqlite ? null : "auth";
        options.EnableTokenCleanup = true;
        options.TokenCleanupInterval = 3600;
    });

var thumbprint = isConfig["SigningCertificateThumbprint"];
if (!string.IsNullOrWhiteSpace(thumbprint))
    identityServer.AddSigningCredential(SigningCredentials.FromStore(thumbprint));
else if (builder.Environment.IsDevelopment())
    identityServer.AddDeveloperSigningCredential(filename: Path.Combine(builder.Environment.ContentRootPath, "../../.devdata/tempkey.jwk"));
else
    throw new InvalidOperationException("IdentityServer:SigningCertificateThumbprint is required.");

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddAuthorization(options =>
    options.AddPolicy("Admin", p => p.RequireRole(Roles.Admin)));

builder.Services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<DatabaseInitializer>();

builder.Services.AddDataProtection()
    .SetApplicationName("ControlTower")
    .PersistKeysToFileSystem(new DirectoryInfo(configuration["DataProtection:KeyPath"] ?? @"D:\ControlTower\Keys\login"));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Manage");
    options.Conventions.AuthorizeFolder("/Admin", "Admin");
});

var app = builder.Build();

if (args.Contains("--initialize", StringComparer.OrdinalIgnoreCase))
{
    return await InitializeAsync(app, args);
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var init = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await init.CreateSchemaAsync();
    await init.SeedRolesAsync();
    await init.SeedDevelopmentUsersAsync();
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
app.UseIdentityServer();
app.UseAuthorization();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
return 0;

static async Task<int> InitializeAsync(WebApplication app, string[] args)
{
    string? Arg(string name)
    {
        var i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    using var scope = app.Services.CreateScope();
    var init = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await init.CreateSchemaAsync();
    await init.SeedRolesAsync();
    Console.WriteLine("Database and roles are in place.");

    var adminUser = Arg("--admin-user");
    var adminPassword = Arg("--admin-password");
    if (adminUser is not null && adminPassword is not null)
    {
        var result = await init.CreateUserAsync(adminUser, Arg("--admin-email") ?? "", adminPassword, Arg("--admin-name") ?? adminUser, Roles.Staff);
        if (!result.Succeeded)
        {
            Console.Error.WriteLine("Administrator could not be created: " + string.Join("; ", result.Errors.Select(e => e.Description)));
            return 1;
        }
        Console.WriteLine($"Administrator '{adminUser}' is in place.");
    }
    return 0;
}

public partial class Program
{
}
