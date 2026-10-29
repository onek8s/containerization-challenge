namespace Litware.DocControl.Platform;

/// <summary>
/// Adds the <c>\\?\</c> prefix so the vault's deep folder structure works past MAX_PATH with the
/// wide-character file APIs.
/// </summary>
public static class LongPath
{
    public static string Of(string path)
    {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return @"\\?\UNC\" + path[2..];
        return @"\\?\" + Path.GetFullPath(path);
    }
}
