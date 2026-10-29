using System.Text;

namespace Proseware.ControlTower.Tests.Integration;

internal static class Samples
{
    public static string Path(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(System.IO.Path.Combine(dir.FullName, "samples")))
            dir = dir.Parent;
        if (dir is null) throw new DirectoryNotFoundException("samples folder not found above " + AppContext.BaseDirectory);
        return System.IO.Path.Combine(new[] { dir.FullName, "samples" }.Concat(parts).ToArray());
    }

    public static string Edifact(string fileName) => File.ReadAllText(Path("edifact", fileName), Encoding.Latin1);

    public static string Text(params string[] parts) => File.ReadAllText(Path(parts));
}
