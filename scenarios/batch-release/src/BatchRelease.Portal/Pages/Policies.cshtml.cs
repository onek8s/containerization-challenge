using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using BatchRelease.Core.Security;
using BatchRelease.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BatchRelease.Portal.Pages;

public class PoliciesModel : PageModel
{
    private readonly BatchQueries _queries;

    public PoliciesModel(BatchQueries queries) => _queries = queries;

    public List<FamilyPolicy> Families { get; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        foreach (var family in await _queries.FamiliesAsync(ct))
        {
            var entries = OperatingSystem.IsWindows() ? Parse(family.PolicySddl) : new List<PolicyEntry> { new(false, "(policy details are shown on the server)", "") };
            Families.Add(new FamilyPolicy(family.Code, family.Name, family.PolicySddl, family.PolicyChangedUtc, family.PolicyChangedBy, entries));
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<PolicyEntry> Parse(string sddl)
    {
        var entries = new List<PolicyEntry>();
        try
        {
            var sd = new RawSecurityDescriptor(sddl);
            foreach (var ace in sd.DiscretionaryAcl!.OfType<CommonAce>())
            {
                var rights = (ReleaseRights)(uint)ace.AccessMask;
                entries.Add(new PolicyEntry(ace.AceQualifier == AceQualifier.AccessDenied, Name(ace.SecurityIdentifier), string.Join(", ", rights.Names())));
            }
        }
        catch (ArgumentException)
        {
            entries.Add(new PolicyEntry(false, "(invalid policy)", ""));
        }
        return entries;
    }

    [SupportedOSPlatform("windows")]
    private static string Name(SecurityIdentifier sid)
    {
        try
        {
            return sid.Translate(typeof(NTAccount)).Value;
        }
        catch (IdentityNotMappedException)
        {
            return sid.Value;
        }
    }

    public sealed record PolicyEntry(bool Deny, string Principal, string Rights);

    public sealed record FamilyPolicy(string Code, string Name, string Sddl, DateTime PolicyChangedUtc, string PolicyChangedBy, List<PolicyEntry> Entries);
}
