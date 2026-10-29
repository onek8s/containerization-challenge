using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Serilog;
using StudyData.Coding;
using StudyData.Core.Services;
using StudyData.Data;
using StudyData.Data.Seed;
using StudyData.Documents;
using StudyData.Monitor.Api.Endpoints;
using StudyData.Monitor.Api.Hubs;
using StudyData.Monitor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, log) =>
{
    log.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(context.Configuration["Logging:LogFile"] ?? "logs/monitor-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 90);
    if (!context.HostingEnvironment.IsDevelopment())
    {
        log.WriteTo.EventLog("StudyData Monitor", manageEventSource: false, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
    }
});

var monitor = builder.Configuration.GetSection(MonitorOptions.Section);
builder.Services.Configure<MonitorOptions>(monitor);
builder.Services.AddStudyData(builder.Configuration);
builder.Services.AddStudyDocuments(builder.Configuration);
builder.Services.AddStudyCoding(builder.Configuration);
// Monitors never save eCRF data; the derivation service is required by the shared data services only.
builder.Services.AddSingleton<IDerivationService, NoDerivationService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.Scheme)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.Scheme, null);
}
else
{
    builder.Services
        .AddAuthentication(o =>
        {
            o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(o =>
        {
            o.Cookie.Name = "StudyData.Monitor";
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.Events.OnRedirectToLogin = ctx =>
            {
                // The SPA calls /api; it gets 401 and reloads the page to sign in.
                if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/hubs"))
                {
                    ctx.Response.StatusCode = 401;
                    return Task.CompletedTask;
                }

                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            };
        })
        .AddOpenIdConnect(o =>
        {
            o.Authority = monitor["Authority"];
            o.ClientId = monitor["ClientId"];
            o.ClientSecret = monitor["ClientSecret"];
            o.ResponseType = "code";
            o.UsePkce = true;
            o.Scope.Add("allatclaims");
            o.SaveTokens = false;
            o.GetClaimsFromUserInfoEndpoint = false;
            o.TokenValidationParameters.NameClaimType = "upn";
            o.TokenValidationParameters.RoleClaimType = "role";
            o.Events.OnTokenValidated = AdfsGroupMapper.MapAsync;
        });
}

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.Review, p => p.RequireRole(Roles.Monitor, Roles.DataManager));
    o.AddPolicy(Policies.DataManagement, p => p.RequireRole(Roles.DataManager));
    o.AddPolicy(Policies.Coding, p => p.RequireRole(Roles.MedicalCoder, Roles.DataManager));
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CasebookPageCache>();
builder.Services.AddHostedService<NotificationRelay>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    StudyData.Data.ServiceCollectionExtensions.EnsureDataDirectory(builder.Configuration.GetConnectionString("StudyData")!);
    await DbInitializer.EnsureCreatedAsync(app.Services, seedSampleData: false);
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = { "app/index.html" } });
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
app.MapGroup("/api").MapMonitorApi();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapFallbackToFile("app/index.html");
app.Run();

public partial class Program;
