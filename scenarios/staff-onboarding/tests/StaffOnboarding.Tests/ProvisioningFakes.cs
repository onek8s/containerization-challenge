using System.Runtime.InteropServices;
using StaffOnboarding.Core.Provisioning;
using StaffOnboarding.Provisioning.Directory;
using StaffOnboarding.Provisioning.HomeFolders;
using StaffOnboarding.Provisioning.Operations;

namespace StaffOnboarding.Tests;

internal sealed class FakeDirectory : IDirectoryService
{
    public sealed class User
    {
        public required NewUser Created { get; init; }
        public string Dn { get; set; } = "";
        public bool Enabled { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, object?> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Groups { get; } = new(StringComparer.OrdinalIgnoreCase) { "Domain Users" };
        public byte[] Sid { get; } = Guid.NewGuid().ToByteArray();
    }

    public Dictionary<string, User> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ExistingGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> TakenNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Exception? FailNextAddToGroup { get; set; }

    public string DomainController => "dc01.corp.local";

    public User AddExisting(string sam, string ou, string? managerSam = null, params string[] groups)
    {
        var user = new User
        {
            Created = new NewUser
            {
                SamAccountName = sam, UserPrincipalName = sam + "@corp.local", Mail = sam + "@litware.com",
                GivenName = sam, Surname = sam, DisplayName = sam, Ou = ou,
                ManagerDn = managerSam == null ? null : $"CN={managerSam},{ou}"
            },
            Dn = $"CN={sam},{ou}",
            Enabled = true
        };
        foreach (var g in groups) user.Groups.Add(g);
        Users[sam] = user;
        return user;
    }

    public DirectoryAccount? FindUser(string samAccountName) =>
        Users.TryGetValue(samAccountName, out var u) ? ToAccount(u) : null;

    public DirectoryAccount? FindCreatedFor(string requestNumber) =>
        Users.Values.Where(u => (u.Attributes.GetValueOrDefault("description") as string ?? u.Created.Description) == "Onboarding " + requestNumber)
            .Select(ToAccount).FirstOrDefault();

    public string? GetSamAccountName(string distinguishedName) =>
        Users.Values.FirstOrDefault(u => u.Dn == distinguishedName)?.Created.SamAccountName;

    public bool NameInUse(string samAccountName, string userPrincipalName, string mail) =>
        Users.ContainsKey(samAccountName) || TakenNames.Contains(samAccountName) ||
        Users.Values.Any(u => string.Equals(u.Created.Mail, mail, StringComparison.OrdinalIgnoreCase));

    public string CreateUser(NewUser user, string initialPassword)
    {
        var dn = $"CN={user.DisplayName},{user.Ou}";
        Users[user.SamAccountName] = new User { Created = user, Dn = dn, Password = initialPassword };
        return dn;
    }

    public void SetEnabled(string samAccountName, bool enabled) => Users[samAccountName].Enabled = enabled;

    public void UpdateAttributes(string samAccountName, IReadOnlyDictionary<string, object?> attributes)
    {
        foreach (var (k, v) in attributes) Users[samAccountName].Attributes[k] = v;
    }

    public void MoveTo(string samAccountName, string ou)
    {
        var u = Users[samAccountName];
        u.Dn = u.Dn.Split(',')[0] + "," + ou;
    }

    public IReadOnlyList<string> GetGroups(string samAccountName) => Users[samAccountName].Groups.OrderBy(g => g).ToList();

    public bool AddToGroup(string samAccountName, string group)
    {
        if (FailNextAddToGroup is { } ex)
        {
            FailNextAddToGroup = null;
            throw ex;
        }
        if (!ExistingGroups.Contains(group)) return false;
        Users[samAccountName].Groups.Add(group);
        return true;
    }

    public void RemoveFromGroup(string samAccountName, string group) => Users[samAccountName].Groups.Remove(group);

    private static DirectoryAccount ToAccount(User u) => new(
        u.Created.SamAccountName, u.Dn, u.Created.DisplayName, u.Created.Mail, u.Created.UserPrincipalName,
        u.Attributes.GetValueOrDefault("department") as string ?? u.Created.Department,
        u.Attributes.GetValueOrDefault("title") as string ?? u.Created.Title,
        u.Attributes.GetValueOrDefault("manager") as string ?? u.Created.ManagerDn,
        u.Attributes.TryGetValue("homeDirectory", out var h) ? h as string : null,
        u.Enabled, u.Sid);

    public static COMException ServerDown() => new("The server is not operational.", unchecked((int)0x8007203A));
}

internal sealed class FakeHomeFolders : IHomeFolderService
{
    public Dictionary<string, byte[]> Created { get; } = new();
    public List<(string Sam, string From, string? HandoverTo)> Archived { get; } = new();
    public Exception? FailCreate { get; set; }

    public string Create(string samAccountName, byte[] userSid)
    {
        if (FailCreate is { } ex)
        {
            FailCreate = null;
            throw ex;
        }
        Created[samAccountName] = userSid;
        return @"\\corp.local\dfs\home\" + samAccountName;
    }

    public string Archive(string samAccountName, string homeDirectory, string? handoverTo)
    {
        Archived.Add((samAccountName, homeDirectory, handoverTo));
        return @"\\corp.local\dfs\archive\home\2026\" + samAccountName;
    }

    public string? ResolveTarget(string path) => null;
}

internal sealed class FakeJournal : IOperationJournal
{
    public List<(string Request, string Step, string? Sam, string Caller, ProvisioningResult Result)> Entries { get; } = new();

    public ProvisioningResult? FindCompleted(string requestNumber, string step) =>
        Entries.LastOrDefault(e => e.Request == requestNumber && e.Step == step && e.Result.Succeeded).Result;

    public void Record(string requestNumber, string step, string? samAccountName, string caller, ProvisioningResult result) =>
        Entries.Add((requestNumber, step, samAccountName, caller, result));
}
