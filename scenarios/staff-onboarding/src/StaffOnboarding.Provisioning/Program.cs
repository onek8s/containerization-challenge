using System.Security.Claims;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using StaffOnboarding.Core.Provisioning;
using StaffOnboarding.Provisioning;
using StaffOnboarding.Provisioning.Directory;
using StaffOnboarding.Provisioning.HomeFolders;
using StaffOnboarding.Provisioning.Operations;
using StaffOnboarding.Provisioning.Provisioning;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Host.UseWindowsService(o => o.ServiceName = "StaffOnboardingIdm");
builder.Logging.AddEventLog(new EventLogSettings { SourceName = "Staff Onboarding Provisioning", LogName = "Application" });

builder.Services.Configure<ProvisioningOptions>(builder.Configuration.GetSection(ProvisioningOptions.Section));
var options = builder.Configuration.GetSection(ProvisioningOptions.Section).Get<ProvisioningOptions>() ?? new ProvisioningOptions();

// Kernel mode Kerberos through http.sys; the SPN HTTP/idm01.corp.local belongs to the computer account.
builder.WebHost.UseHttpSys(o =>
{
    o.Authentication.Schemes = AuthenticationSchemes.Negotiate;
    o.Authentication.AllowAnonymous = false;
    o.MaxRequestBodySize = 256 * 1024;
    foreach (var prefix in options.UrlPrefixes.DefaultIfEmpty("http://+:8090/")) o.UrlPrefixes.Add(prefix);
});

builder.Services.AddAuthentication(HttpSysDefaults.AuthenticationScheme);
builder.Services.AddAuthorization(a =>
{
    a.AddPolicy("Provision", p => p.RequireAssertion(ctx => IsCaller(ctx.User, options.AllowedCallers)));
    a.AddPolicy("Read", p => p.RequireAssertion(ctx =>
        IsCaller(ctx.User, options.AllowedCallers) || IsCaller(ctx.User, options.ReadOnlyCallers)));
});

builder.Services.AddSingleton<IDirectoryService, AdDirectoryService>();
builder.Services.AddSingleton<IHomeFolderService, NtfsHomeFolderService>();
builder.Services.AddSingleton<IOperationJournal, SqlOperationJournal>();
builder.Services.AddSingleton<AccountProvisioner>();

// Directory writes are kept in order: one command at a time against the pinned domain controller.
builder.Services.AddSingleton(new SemaphoreSlim(1, 1));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", (IDirectoryService directory, IOptions<ProvisioningOptions> o) => Results.Ok(new
{
    status = "ok",
    domainController = directory.DomainController,
    homeRoot = o.Value.HomeRoot,
    homeRootReachable = System.IO.Directory.Exists(o.Value.HomeRoot)
})).RequireAuthorization("Read");

var api = app.MapGroup("/api/accounts");

api.MapGet("/{sam}", (string sam, AccountProvisioner p) =>
    p.Get(sam) is { } account ? Results.Ok(account) : Results.NotFound()).RequireAuthorization("Read");

api.MapPost("/", (CreateAccountCommand c, HttpContext h, AccountProvisioner p, SemaphoreSlim gate) =>
    Execute(gate, () => p.Create(c, Caller(h)))).RequireAuthorization("Provision");

api.MapPost("/{sam}/enable", (string sam, EnableAccountCommand c, HttpContext h, AccountProvisioner p, SemaphoreSlim gate) =>
    Execute(gate, () => p.Enable(Bind(c, sam), Caller(h)))).RequireAuthorization("Provision");

api.MapPost("/{sam}/access", (string sam, UpdateAccessCommand c, HttpContext h, AccountProvisioner p, SemaphoreSlim gate) =>
    Execute(gate, () => p.UpdateAccess(Bind(c, sam), Caller(h)))).RequireAuthorization("Provision");

api.MapPost("/{sam}/disable", (string sam, DisableAccountCommand c, HttpContext h, AccountProvisioner p, SemaphoreSlim gate) =>
    Execute(gate, () => p.Disable(Bind(c, sam), Caller(h)))).RequireAuthorization("Provision");

api.MapPost("/{sam}/archive", (string sam, ArchiveAccountCommand c, HttpContext h, AccountProvisioner p, SemaphoreSlim gate) =>
    Execute(gate, () => p.Archive(Bind(c, sam), Caller(h)))).RequireAuthorization("Provision");

app.Run();

static bool IsCaller(ClaimsPrincipal user, IEnumerable<string> allowed) =>
    user.Identity?.IsAuthenticated == true &&
    allowed.Any(a => string.Equals(a, user.Identity.Name, StringComparison.OrdinalIgnoreCase) || user.IsInRole(a));

static string Caller(HttpContext h) => h.User.Identity?.Name ?? "unknown";

static async Task<IResult> Execute(SemaphoreSlim gate, Func<ProvisioningResult> action)
{
    await gate.WaitAsync();
    try
    {
        var result = action();
        return result.Succeeded || result.Retryable ? Results.Ok(result) : Results.UnprocessableEntity(result);
    }
    finally
    {
        gate.Release();
    }
}

static T Bind<T>(T command, string sam) where T : ProvisioningCommand
{
    switch (command)
    {
        case EnableAccountCommand e: e.SamAccountName = sam; break;
        case UpdateAccessCommand u: u.SamAccountName = sam; break;
        case DisableAccountCommand d: d.SamAccountName = sam; break;
        case ArchiveAccountCommand a: a.SamAccountName = sam; break;
    }
    return command;
}

public partial class Program;
