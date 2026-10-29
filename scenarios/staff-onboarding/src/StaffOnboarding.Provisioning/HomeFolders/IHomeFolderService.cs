namespace StaffOnboarding.Provisioning.HomeFolders;

public interface IHomeFolderService
{
    /// <summary>Creates (or repairs the permissions of) the user's home folder and returns its UNC path.</summary>
    string Create(string samAccountName, byte[] userSid);

    /// <summary>Moves a home folder to the archive and grants read access to <paramref name="handoverTo"/>.</summary>
    string Archive(string samAccountName, string homeDirectory, string? handoverTo);

    /// <summary>Returns the file server folder a DFS path currently refers to, for the audit trail.</summary>
    string? ResolveTarget(string path);
}
