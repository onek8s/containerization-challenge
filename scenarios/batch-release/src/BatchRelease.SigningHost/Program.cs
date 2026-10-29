using BatchRelease.Data;
using BatchRelease.SigningHost;
using BatchRelease.SigningHost.Commands;
using BatchRelease.SigningHost.Endpoints;
using BatchRelease.SigningHost.Signing;
using BatchRelease.Vault;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Server.HttpSys;

if (AdminCommands.IsCommand(args))
    return await AdminCommands.RunAsync(args);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Host.UseWindowsService(o => o.ServiceName = "BatchReleaseSigning");
builder.Logging.AddEventLog(o =>
{
    o.LogName = "Application";
    o.SourceName = "Batch Release Signing";
});

builder.WebHost.UseHttpSys(o =>
{
    o.Authentication.Schemes = AuthenticationSchemes.Negotiate;
    o.Authentication.AllowAnonymous = false;
    o.MaxRequestBodySize = 210L * 1024 * 1024;
    var urls = builder.Configuration["SigningHost:Urls"] ?? "https://release-sign.corp.local:5443/";
    foreach (var url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        o.UrlPrefixes.Add(url);
});

builder.Services.Configure<SigningOptions>(builder.Configuration.GetSection(SigningOptions.Section));
builder.Services.Configure<VaultOptions>(builder.Configuration.GetSection(VaultOptions.Section));

builder.Services.AddBatchReleaseData(builder.Configuration);
// The portal and API send enums as strings (SigningHostClient.Json).
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSingleton<CredentialVerifier>();
builder.Services.AddSingleton<SigningKeyStore>();
builder.Services.AddSingleton<AccessEvaluator>();
builder.Services.AddSingleton<RecordVault>();
builder.Services.AddSingleton<SecurityAudit>();
builder.Services.AddScoped<DecisionService>();

builder.Services.AddAuthentication(HttpSysDefaults.AuthenticationScheme);
builder.Services.AddSingleton<IAuthorizationHandler, TrustedCallerHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(TrustedCallerRequirement.Policy, p => p.RequireAuthenticatedUser().AddRequirements(new TrustedCallerRequirement()));

var app = builder.Build();

// Fail at start-up rather than on the first signature.
app.Services.GetRequiredService<RecordVault>().EnsureReady();
app.Services.GetRequiredService<SecurityAudit>();
app.Services.GetRequiredService<AccessEvaluator>();

app.UseAuthentication();
app.UseAuthorization();
app.MapSigningEndpoints();

await app.RunAsync();
return 0;
