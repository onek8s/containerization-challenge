using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace StaffOnboarding.Portal.Services;

/// <summary>The signed-in Windows user and the onboarding roles they hold.</summary>
public sealed class CurrentUser
{
    private readonly ClaimsPrincipal _user;
    private readonly PortalOptions _options;

    public CurrentUser(IHttpContextAccessor http, IOptions<PortalOptions> options)
    {
        _user = http.HttpContext?.User ?? new ClaimsPrincipal();
        _options = options.Value;
    }

    /// <summary>DOMAIN\account as given by Windows authentication.</summary>
    public string Name => _user.Identity?.Name ?? "anonymous";

    /// <summary>sAMAccountName without the domain.</summary>
    public string Account
    {
        get
        {
            var name = Name;
            var i = name.IndexOf('\\');
            return (i >= 0 ? name[(i + 1)..] : name).ToLowerInvariant();
        }
    }

    public bool IsHr => InGroup(_options.HrGroup);
    public bool IsIt => InGroup(_options.ItGroup);

    public bool InGroup(string group) => _user.IsInRole(_options.Domain + "\\" + group);

    /// <summary>Whether a task assigned to "account" or "group:name" belongs to this user.</summary>
    public bool IsAssignee(string assignedTo) =>
        assignedTo.StartsWith("group:", StringComparison.OrdinalIgnoreCase)
            ? InGroup(assignedTo[6..])
            : string.Equals(assignedTo, Account, StringComparison.OrdinalIgnoreCase);

    public IEnumerable<string> GroupAssignees(IEnumerable<string> candidates) =>
        candidates.Where(c => c.StartsWith("group:", StringComparison.OrdinalIgnoreCase) && InGroup(c[6..]));
}
