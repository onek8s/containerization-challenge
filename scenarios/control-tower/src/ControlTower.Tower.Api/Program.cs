using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using NLog.Web;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Tower.Auth;
using Proseware.ControlTower.Tower.Hubs;
using Proseware.ControlTower.Tower.Live;
using Proseware.ControlTower.Tower.Services;

var logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    var services = builder.Services;
    var configuration = builder.Configuration;

    services.AddControlTowerData(configuration);
    services.AddControlTowerMessaging(configuration);
    services.AddSingleton<IClock, Proseware.ControlTower.Domain.SystemClock>();
    services.AddScoped<DashboardService>();
    services.AddScoped<KpiService>();
    services.AddScoped<TerminalWorkloadService>();
    services.AddScoped<AlertService>();

    var keyFolder = configuration["DataProtection:KeyFolder"];
    var dp = services.AddDataProtection().SetApplicationName("ControlTower.Tower");
    if (!string.IsNullOrEmpty(keyFolder)) dp.PersistKeysToFileSystem(new DirectoryInfo(keyFolder));

    JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
    var useDevAuth = builder.Environment.IsDevelopment() && configuration.GetValue("Authentication:UseDevelopmentUser", true);
    if (useDevAuth)
    {
        services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, null);
    }
    else
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Authentication:Authority"];
                options.Audience = configuration["Authentication:Audience"] ?? "tower.api";
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "role";
                // SignalR sends the token in the query string for WebSockets and server-sent events.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });
    }

    services.AddAuthorization(options =>
    {
        options.AddPolicy(Policies.Operations, p => p.RequireAuthenticatedUser().RequireRole("ops", "admin"));
        options.AddPolicy(Policies.Admin, p => p.RequireAuthenticatedUser().RequireRole("admin"));
        options.FallbackPolicy = options.GetPolicy(Policies.Operations);
    });

    services.AddControllers().AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
    services.AddSignalR().AddJsonProtocol(o =>
    {
        o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
    services.AddHostedService<LiveUpdateRelay>();

    services.AddCors(o => o.AddPolicy("spa-dev", p => p
        .WithOrigins(configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>())
        .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Control Tower API", Version = "v1" });
        var xml = Path.Combine(AppContext.BaseDirectory, "ControlTower.Tower.Api.xml");
        if (File.Exists(xml)) c.IncludeXmlComments(xml);
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
        });
    });

    services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        if (string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var file = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(configuration.GetConnectionString("ControlTower")).DataSource;
            var folder = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        }
        await app.Services.EnsureDevelopmentDatabaseAsync(seed: true);
        app.UseDeveloperExceptionPage();
        app.UseCors("spa-dev");
    }
    else
    {
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }

    app.UseForwardedHeaders();
    app.UseHttpsRedirection();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Control Tower API v1"));
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<TowerHub>("/hubs/tower");
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy", time = DateTimeOffset.UtcNow })).AllowAnonymous();
    // Sign-in settings for the SPA; the bundle itself carries no environment specific values.
    app.MapGet("/api/config", () => Results.Ok(new
    {
        authority = configuration["Spa:Authority"],
        clientId = configuration["Spa:ClientId"],
        scope = configuration["Spa:Scope"],
        mapTileUrl = configuration["Spa:MapTileUrl"] ?? "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
        developmentSignIn = useDevAuth
    })).AllowAnonymous();
    app.Map("/error", () => Results.Problem()).AllowAnonymous();
    app.MapGet("/", () => Results.Redirect("/app/")).AllowAnonymous();
    app.MapFallbackToFile("/app/{**path}", "app/index.html").AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex.GetType().Name != "StopTheHostException")
{
    logger.Error(ex, "Control Tower API stopped because of an exception");
    throw;
}
finally
{
    NLog.LogManager.Shutdown();
}

public partial class Program
{
}
