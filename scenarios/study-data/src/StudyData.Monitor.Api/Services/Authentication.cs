using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using StudyData.Data;

namespace StudyData.Monitor.Api.Services;

/// <summary>ADFS sends AD group names in the "group" claim (issuance transform rule "Token-Groups - Unqualified Names").</summary>
public static class AdfsGroupMapper
{
    public static Task MapAsync(TokenValidatedContext context)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<MonitorOptions>>().Value;
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        var groups = identity.FindAll("group").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (role, group) in options.Groups)
        {
            if (groups.Contains(group))
            {
                identity.AddClaim(new Claim("role", role));
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>Development sign-in without ADFS: the configured user with every role (?as=user&amp;roles=Monitor to change).</summary>
public class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public new const string Scheme = "Development";

    private readonly MonitorOptions _monitor;

    public DevelopmentAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        IOptions<MonitorOptions> monitor)
        : base(options, logger, encoder)
    {
        _monitor = monitor.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var name = Request.Query["as"].FirstOrDefault() ?? _monitor.DevelopmentUser ?? "CORP\\monitor1";
        var roles = Request.Query["roles"].FirstOrDefault()?.Split(',')
            ?? new[] { Roles.Monitor, Roles.DataManager, Roles.MedicalCoder, Roles.StudyAdmin };
        var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.Name, name));
        foreach (var r in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, r));
        }

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
