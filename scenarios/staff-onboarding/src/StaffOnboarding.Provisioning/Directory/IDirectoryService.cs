namespace StaffOnboarding.Provisioning.Directory;

public sealed record DirectoryAccount(
    string SamAccountName,
    string DistinguishedName,
    string? DisplayName,
    string? Mail,
    string? UserPrincipalName,
    string? Department,
    string? Title,
    string? ManagerDn,
    string? HomeDirectory,
    bool Enabled,
    byte[] Sid);

public sealed class NewUser
{
    public required string SamAccountName { get; init; }
    public required string UserPrincipalName { get; init; }
    public required string Mail { get; init; }
    public required string GivenName { get; init; }
    public required string Surname { get; init; }
    public required string DisplayName { get; init; }
    public required string Ou { get; init; }
    public string? EmployeeId { get; init; }
    public string? EmployeeType { get; init; }
    public string? Department { get; init; }
    public string? Title { get; init; }
    public string? Office { get; init; }
    public string? CostCenter { get; init; }
    public string? Company { get; init; }
    public string? MobilePhone { get; init; }
    public string? ManagerDn { get; init; }
    public DateTime? AccountExpiresUtc { get; init; }
    public string? Description { get; init; }
}

/// <summary>Active Directory operations used by provisioning. All calls go to one domain controller.</summary>
public interface IDirectoryService
{
    string DomainController { get; }

    DirectoryAccount? FindUser(string samAccountName);

    /// <summary>Finds an account created for a request (description "Onboarding &lt;request&gt;").</summary>
    DirectoryAccount? FindCreatedFor(string requestNumber);

    /// <summary>sAMAccountName of the object with this distinguished name, or null.</summary>
    string? GetSamAccountName(string distinguishedName);

    /// <summary>True when sAMAccountName, UPN or mail/proxyAddresses already use the name.</summary>
    bool NameInUse(string samAccountName, string userPrincipalName, string mail);

    /// <summary>Creates a disabled user with the given password that must be changed at first sign-in.</summary>
    string CreateUser(NewUser user, string initialPassword);

    void SetEnabled(string samAccountName, bool enabled);
    void UpdateAttributes(string samAccountName, IReadOnlyDictionary<string, object?> attributes);
    void MoveTo(string samAccountName, string ou);

    IReadOnlyList<string> GetGroups(string samAccountName);

    /// <summary>Returns false when the group does not exist.</summary>
    bool AddToGroup(string samAccountName, string group);

    void RemoveFromGroup(string samAccountName, string group);
}
