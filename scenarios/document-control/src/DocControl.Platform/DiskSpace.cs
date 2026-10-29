using Litware.DocControl.Platform.Interop;

namespace Litware.DocControl.Platform;

public readonly record struct DiskSpaceInfo(ulong FreeBytesAvailable, ulong TotalBytes, ulong TotalFreeBytes)
{
    public double FreePercent => TotalBytes == 0 ? 0 : FreeBytesAvailable * 100.0 / TotalBytes;
}

public static class DiskSpace
{
    /// <summary>
    /// Free space for a local folder or a UNC share path (DriveInfo only handles drive letters).
    /// Takes the caller's quota into account.
    /// </summary>
    public static DiskSpaceInfo Get(string path)
    {
        var folder = path.EndsWith('\\') ? path : path + '\\';
        if (!NativeMethods.GetDiskFreeSpaceEx(folder, out var available, out var total, out var free))
            throw Win32Exceptions.ForLastError("GetDiskFreeSpaceEx", path);
        return new DiskSpaceInfo(available, total, free);
    }
}
