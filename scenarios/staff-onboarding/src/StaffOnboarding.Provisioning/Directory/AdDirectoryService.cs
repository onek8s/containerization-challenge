using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Options;
using AdDomainController = System.DirectoryServices.ActiveDirectory.DomainController;

namespace StaffOnboarding.Provisioning.Directory;

/// <summary>
/// LDAP writes with the service's own Kerberos identity (the gMSA). Signing and sealing are required
/// for SetPassword and by the domain controllers' LDAP signing policy.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AdDirectoryService : IDirectoryService, IDisposable
{
    private const int UfAccountDisable = 0x0002;
    private const int UfNormalAccount = 0x0200;
    private const AuthenticationTypes Auth = AuthenticationTypes.Secure | AuthenticationTypes.Signing |
                                             AuthenticationTypes.Sealing | AuthenticationTypes.ServerBind;

    private static readonly string[] AccountProperties =
    {
        "sAMAccountName", "distinguishedName", "displayName", "mail", "userPrincipalName", "department", "title",
        "manager", "homeDirectory", "userAccountControl", "objectSid"
    };

    private readonly ILogger<AdDirectoryService> _logger;
    private readonly string _namingContext;
    private readonly PrincipalContext _principals;

    public AdDirectoryService(IOptions<ProvisioningOptions> options, ILogger<AdDirectoryService> logger)
    {
        _logger = logger;
        var o = options.Value;
        DomainController = string.IsNullOrWhiteSpace(o.DomainController) ? LocateWritableDc(o) : o.DomainController!;

        using (var rootDse = new DirectoryEntry($"LDAP://{DomainController}/RootDSE", null, null, Auth))
            _namingContext = (string)rootDse.Properties["defaultNamingContext"].Value!;

        _principals = new PrincipalContext(ContextType.Domain, DomainController, _namingContext,
            ContextOptions.Negotiate | ContextOptions.Signing | ContextOptions.Sealing);

        _logger.LogInformation("Using domain controller {DomainController} for {NamingContext}", DomainController, _namingContext);
    }

    public string DomainController { get; }

    public DirectoryAccount? FindUser(string samAccountName)
    {
        using var searcher = Searcher($"(&(objectCategory=person)(objectClass=user)(sAMAccountName={Escape(samAccountName)}))");
        var result = searcher.FindOne();
        return result == null ? null : ToAccount(result);
    }

    public DirectoryAccount? FindCreatedFor(string requestNumber)
    {
        using var searcher = Searcher($"(&(objectCategory=person)(objectClass=user)(description=Onboarding {Escape(requestNumber)}))");
        var result = searcher.FindOne();
        return result == null ? null : ToAccount(result);
    }

    public string? GetSamAccountName(string distinguishedName)
    {
        try
        {
            using var entry = Entry(distinguishedName);
            return entry.Properties["sAMAccountName"].Value as string;
        }
        catch (DirectoryServicesCOMException ex) when (ex.ExtendedError == 8333) // ERROR_DS_OBJ_NOT_FOUND
        {
            return null;
        }
    }

    public bool NameInUse(string samAccountName, string userPrincipalName, string mail)
    {
        var filter = $"(|(sAMAccountName={Escape(samAccountName)})(userPrincipalName={Escape(userPrincipalName)})" +
                     $"(mail={Escape(mail)})(proxyAddresses=smtp:{Escape(mail)}))";
        using var searcher = Searcher(filter);
        searcher.SizeLimit = 1;
        return searcher.FindOne() != null;
    }

    public string CreateUser(NewUser user, string initialPassword)
    {
        using var parent = Entry(user.Ou);
        var cn = UniqueCn(parent, user.DisplayName, user.SamAccountName);
        using var entry = parent.Children.Add("CN=" + EscapeRdn(cn), "user");

        Set(entry, "sAMAccountName", user.SamAccountName);
        Set(entry, "userPrincipalName", user.UserPrincipalName);
        Set(entry, "givenName", user.GivenName);
        Set(entry, "sn", user.Surname);
        Set(entry, "displayName", user.DisplayName);
        Set(entry, "mail", user.Mail);
        Set(entry, "employeeID", user.EmployeeId);
        Set(entry, "employeeType", user.EmployeeType);
        Set(entry, "department", user.Department);
        Set(entry, "title", user.Title);
        Set(entry, "physicalDeliveryOfficeName", user.Office);
        Set(entry, "extensionAttribute1", user.CostCenter);
        Set(entry, "company", user.Company);
        Set(entry, "mobile", user.MobilePhone);
        Set(entry, "manager", user.ManagerDn);
        Set(entry, "description", user.Description);
        entry.Properties["userAccountControl"].Value = UfNormalAccount | UfAccountDisable;
        entry.CommitChanges();

        entry.Invoke("SetPassword", initialPassword);
        entry.Properties["pwdLastSet"].Value = 0;
        if (user.AccountExpiresUtc.HasValue)
            entry.Properties["accountExpires"].Value = user.AccountExpiresUtc.Value.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture);
        entry.CommitChanges();

        var dn = (string)entry.Properties["distinguishedName"].Value!;
        _logger.LogInformation("Created {Sam} as {Dn} on {Dc}", user.SamAccountName, dn, DomainController);
        return dn;
    }

    public void SetEnabled(string samAccountName, bool enabled)
    {
        using var entry = UserEntry(samAccountName);
        var uac = (int)entry.Properties["userAccountControl"].Value!;
        entry.Properties["userAccountControl"].Value = enabled ? uac & ~UfAccountDisable : uac | UfAccountDisable;
        entry.CommitChanges();
    }

    public void UpdateAttributes(string samAccountName, IReadOnlyDictionary<string, object?> attributes)
    {
        using var entry = UserEntry(samAccountName);
        foreach (var (name, value) in attributes)
        {
            if (value is null || value is string { Length: 0 })
                entry.Properties[name].Clear();
            else if (value is DateTime utc)
                entry.Properties[name].Value = utc.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture);
            else
                entry.Properties[name].Value = value;
        }
        entry.CommitChanges();
    }

    public void MoveTo(string samAccountName, string ou)
    {
        using var entry = UserEntry(samAccountName);
        var current = (string)entry.Properties["distinguishedName"].Value!;
        if (current.EndsWith("," + ou, StringComparison.OrdinalIgnoreCase)) return;
        using var target = Entry(ou);
        entry.MoveTo(target);
        _logger.LogInformation("Moved {Sam} to {Ou}", samAccountName, ou);
    }

    public IReadOnlyList<string> GetGroups(string samAccountName)
    {
        using var user = UserPrincipal.FindByIdentity(_principals, IdentityType.SamAccountName, samAccountName)
                         ?? throw new KeyNotFoundException($"User {samAccountName} not found.");
        using var groups = user.GetGroups();
        return groups.Select(g => g.SamAccountName ?? g.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool AddToGroup(string samAccountName, string group)
    {
        using var g = GroupPrincipal.FindByIdentity(_principals, IdentityType.SamAccountName, group);
        if (g == null) return false;
        if (!g.Members.Contains(_principals, IdentityType.SamAccountName, samAccountName))
        {
            g.Members.Add(_principals, IdentityType.SamAccountName, samAccountName);
            g.Save();
        }
        return true;
    }

    public void RemoveFromGroup(string samAccountName, string group)
    {
        using var g = GroupPrincipal.FindByIdentity(_principals, IdentityType.SamAccountName, group);
        if (g == null) return;
        if (g.Members.Remove(_principals, IdentityType.SamAccountName, samAccountName))
            g.Save();
    }

    public void Dispose() => _principals.Dispose();

    private static string LocateWritableDc(ProvisioningOptions o)
    {
        var context = new DirectoryContext(DirectoryContextType.Domain, o.Domain);
        using AdDomainController dc = string.IsNullOrWhiteSpace(o.Site)
            ? AdDomainController.FindOne(context, LocatorOptions.WriteableRequired | LocatorOptions.ForceRediscovery)
            : AdDomainController.FindOne(context, o.Site, LocatorOptions.WriteableRequired | LocatorOptions.ForceRediscovery);
        return dc.Name;
    }

    private DirectoryEntry Entry(string dn) => new($"LDAP://{DomainController}/{dn}", null, null, Auth);

    private DirectorySearcher Searcher(string filter)
    {
        var searcher = new DirectorySearcher(Entry(_namingContext), filter, AccountProperties, SearchScope.Subtree);
        searcher.PageSize = 100;
        return searcher;
    }

    private DirectoryEntry UserEntry(string samAccountName)
    {
        using var searcher = Searcher($"(&(objectCategory=person)(objectClass=user)(sAMAccountName={Escape(samAccountName)}))");
        var result = searcher.FindOne() ?? throw new KeyNotFoundException($"User {samAccountName} not found.");
        return result.GetDirectoryEntry();
    }

    private string UniqueCn(DirectoryEntry parent, string displayName, string sam)
    {
        using var searcher = new DirectorySearcher(parent, $"(cn={Escape(displayName)})", new[] { "cn" }, SearchScope.OneLevel);
        return searcher.FindOne() == null ? displayName : $"{displayName} ({sam})";
    }

    private static DirectoryAccount ToAccount(SearchResult r)
    {
        string? S(string name) => r.Properties[name].Count > 0 ? r.Properties[name][0]?.ToString() : null;
        var uac = r.Properties["userAccountControl"].Count > 0 ? (int)r.Properties["userAccountControl"][0] : 0;
        return new DirectoryAccount(
            S("sAMAccountName")!, S("distinguishedName")!, S("displayName"), S("mail"), S("userPrincipalName"),
            S("department"), S("title"), S("manager"), S("homeDirectory"), (uac & UfAccountDisable) == 0,
            (byte[])r.Properties["objectSid"][0]);
    }

    private static void Set(DirectoryEntry entry, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) entry.Properties[name].Value = value;
    }

    /// <summary>RFC 4515 filter escaping.</summary>
    internal static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\5c"); break;
                case '*': sb.Append(@"\2a"); break;
                case '(': sb.Append(@"\28"); break;
                case ')': sb.Append(@"\29"); break;
                case '\0': sb.Append(@"\00"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>RFC 4514 RDN value escaping.</summary>
    internal static string EscapeRdn(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (",+\"\\<>;=/".Contains(c) || (i == 0 && (c == '#' || c == ' ')) || (i == value.Length - 1 && c == ' '))
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
