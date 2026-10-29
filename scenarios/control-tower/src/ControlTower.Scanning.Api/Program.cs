using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.OpenApi.Models;
using NLog.Web;
using Proseware.ControlTower.Data;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Integration.Messaging;
using Proseware.ControlTower.Scanning;
using Proseware.ControlTower.Scanning.Auth;
using Proseware.ControlTower.Scanning.Labels;
using Proseware.ControlTower.Scanning.Scans;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
if (builder.Environment.IsDevelopment())
    NLog.LogManager.Configuration.Variables["logDir"] = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../.devdata/logs"));

var configuration = builder.Configuration;

builder.Services.AddControlTowerData(configuration);
builder.Services.AddControlTowerMessaging(configuration);
builder.Services.AddControlTowerLabelPrinting(configuration);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ScanService>();
builder.Services.AddScoped<LabelService>();
builder.Services.AddHostedService<LabelPrintListener>();

builder.Services.AddDataProtection()
    .SetApplicationName("ControlTower")
    .PersistKeysToFileSystem(new DirectoryInfo(configuration["DataProtection:KeyPath"] ?? @"D:\ControlTower\Keys\scan"));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Authentication:Authority"];
        options.Audience = "scan.api";
        options.RequireHttpsMetadata = configuration.GetValue("Authentication:RequireHttpsMetadata", true);
        options.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
    })
    .AddScheme<ApiKeyOptions, ApiKeyAuthenticationHandler>(ApiKeyOptions.Scheme, options =>
        configuration.GetSection("DeviceKeys").Bind(options.Devices));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Scanner", policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyOptions.Scheme)
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => ctx.User.FindAll("scope").Any(c => c.Value.Split(' ').Contains("scan.api"))));
});

// Handheld app sends and expects enum names ("Receive", "Accepted").
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Control Tower scanning API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityDefinition("DeviceKey", new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = ApiKeyOptions.HeaderName });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>(),
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "DeviceKey" } }] = Array.Empty<string>()
    });
});
builder.Services.AddHealthChecks().AddDbContextCheck<ControlTowerDbContext>("database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
    await app.Services.EnsureDevelopmentDatabaseAsync(seed: true);

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Scanning API v1"));
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecks("/health");
app.MapScanEndpoints();

app.Run();

public partial class Program
{
}
