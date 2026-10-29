using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Options;
using StaffOnboarding.Provisioning.Interop;

namespace StaffOnboarding.Provisioning.HomeFolders;

/// <summary>
/// Home folders on the DFS namespace. Permissions use the new account's SID, not its name, because the
/// file server's domain controller may not have the account yet right after it was created.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class NtfsHomeFolderService : IHomeFolderService
{
    private const InheritanceFlags Inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

    private readonly ProvisioningOptions _options;
    private readonly ILogger<NtfsHomeFolderService> _logger;

    public NtfsHomeFolderService(IOptions<ProvisioningOptions> options, ILogger<NtfsHomeFolderService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string Create(string samAccountName, byte[] userSid)
    {
        var path = Path.Combine(_options.HomeRoot, samAccountName);
        var folder = new DirectoryInfo(path);
        if (!folder.Exists) folder.Create();

        var user = new SecurityIdentifier(userSid, 0);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(Rule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl));
        security.AddAccessRule(Rule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl));
        security.AddAccessRule(Rule(new NTAccount(_options.FileAdminsGroup), FileSystemRights.FullControl));
        security.AddAccessRule(Rule(user, FileSystemRights.Modify | FileSystemRights.Synchronize));

        if (_options.SetHomeFolderOwner)
        {
            using (Privileges.Enable(Privileges.SeRestorePrivilege))
            {
                security.SetOwner(user);
                folder.SetAccessControl(security);
            }
        }
        else
        {
            folder.SetAccessControl(security);
        }

        _logger.LogInformation("Home folder {Path} ready for {Sam} (target {Target})", path, samAccountName, ResolveTarget(path));
        return path;
    }

    public string Archive(string samAccountName, string homeDirectory, string? handoverTo)
    {
        var year = DateTime.Today.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var target = Path.Combine(_options.ArchiveRoot, year, samAccountName);

        if (System.IO.Directory.Exists(homeDirectory))
        {
            // The archive is on a different DFS target than the home folders, so a rename is not possible.
            CopyTree(new DirectoryInfo(homeDirectory), new DirectoryInfo(target));
            System.IO.Directory.Delete(homeDirectory, recursive: true);
        }
        else if (!System.IO.Directory.Exists(target))
        {
            throw new DirectoryNotFoundException($"Home folder {homeDirectory} does not exist and has not been archived.");
        }

        if (!string.IsNullOrWhiteSpace(handoverTo))
        {
            var folder = new DirectoryInfo(target);
            var security = folder.GetAccessControl();
            security.AddAccessRule(Rule(new NTAccount(_options.NetBiosDomain, handoverTo), FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize));
            folder.SetAccessControl(security);
        }

        _logger.LogInformation("Archived {Source} to {Target}", homeDirectory, target);
        return target;
    }

    public string? ResolveTarget(string path)
    {
        try
        {
            return Dfs.GetActiveTarget(path);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve DFS target of {Path}", path);
            return null;
        }
    }

    private static FileSystemAccessRule Rule(IdentityReference identity, FileSystemRights rights) =>
        new(identity, rights, Inherit, PropagationFlags.None, AccessControlType.Allow);

    private static void CopyTree(DirectoryInfo source, DirectoryInfo target)
    {
        target.Create();
        foreach (var file in source.EnumerateFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite: true);
        foreach (var dir in source.EnumerateDirectories())
            CopyTree(dir, new DirectoryInfo(Path.Combine(target.FullName, dir.Name)));
    }
}
