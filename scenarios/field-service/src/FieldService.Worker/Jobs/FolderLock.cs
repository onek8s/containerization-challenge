namespace FieldService.Worker.Jobs;

/// <summary>
/// Exclusive lock file so that only one worker instance processes a folder at a time.
/// </summary>
public sealed class FolderLock : IDisposable
{
    private readonly FileStream _stream;

    private FolderLock(FileStream stream)
    {
        _stream = stream;
    }

    public static FolderLock? TryAcquire(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".lock");
        try
        {
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write($"{Environment.MachineName} {Environment.ProcessId} {DateTime.Now:O}");
            return new FolderLock(stream);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _stream.Dispose();
}
