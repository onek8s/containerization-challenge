using System.Runtime.InteropServices;
using System.Text;

namespace Litware.DocControl.Platform;

public sealed record DocTrackArchiveEntry(int Index, string Name, long Size, DateTime ModifiedUtc);

/// <summary>
/// Reader for transmittal archives (<c>.dta</c>) from the DocTrack 5 system that was used until 2019.
/// The format is proprietary; the reader library <c>dtzip.dll</c> is installed with the DocTrack
/// viewer and copied into the Indexer folder. Only needed for the archive import command.
/// </summary>
public sealed class DocTrackArchive : IDisposable
{
    private const string Library = "dtzip.dll";

    private IntPtr _handle;

    [DllImport(Library, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    private static extern int DtzOpen(string path, string? password, out IntPtr archive);

    [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
    private static extern int DtzGetCount(IntPtr archive);

    [DllImport(Library, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    private static extern int DtzGetEntry(IntPtr archive, int index, StringBuilder name, int nameLength, out long size, out long modifiedFileTime);

    [DllImport(Library, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    private static extern int DtzExtract(IntPtr archive, int index, string targetPath);

    [DllImport(Library, CallingConvention = CallingConvention.StdCall)]
    private static extern int DtzClose(IntPtr archive);

    [DllImport(Library, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    private static extern int DtzGetLastError(StringBuilder message, int length);

    public DocTrackArchive(string path, string? password = null)
    {
        Check(DtzOpen(path, password, out _handle), path);
        Path = path;
    }

    public string Path { get; }

    public IReadOnlyList<DocTrackArchiveEntry> Entries()
    {
        var count = DtzGetCount(_handle);
        var list = new List<DocTrackArchiveEntry>(count);
        var name = new StringBuilder(520);
        for (var i = 0; i < count; i++)
        {
            name.Clear();
            Check(DtzGetEntry(_handle, i, name, name.Capacity, out var size, out var ft), Path);
            list.Add(new DocTrackArchiveEntry(i, name.ToString(), size, DateTime.FromFileTimeUtc(ft)));
        }
        return list;
    }

    public void Extract(DocTrackArchiveEntry entry, string targetPath)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetPath)!);
        Check(DtzExtract(_handle, entry.Index, targetPath), Path);
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            DtzClose(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private static void Check(int result, string path)
    {
        if (result == 0) return;
        var sb = new StringBuilder(512);
        DtzGetLastError(sb, sb.Capacity);
        throw new IOException($"DocTrack archive '{path}': {sb} ({result})");
    }
}
