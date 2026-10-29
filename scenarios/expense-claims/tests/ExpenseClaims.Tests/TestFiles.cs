namespace ExpenseClaims.Tests;

internal static class TestFiles
{
    /// <summary>Path of a file under the scenario's samples folder.</summary>
    public static string Sample(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
            dir = dir.Parent;
        if (dir is null)
            throw new DirectoryNotFoundException("samples folder not found above " + AppContext.BaseDirectory);
        return Path.Combine(new[] { dir.FullName, "samples" }.Concat(parts).ToArray());
    }
}
