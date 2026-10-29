using Litware.DocControl.Platform.Interop;

namespace Litware.DocControl.Platform;

public static class FileOps
{
    /// <summary>Files above this size are copied unbuffered with CopyFileEx (large IFC and DWG sets).</summary>
    public const long LargeFileThreshold = 32L * 1024 * 1024;

    /// <summary>
    /// Copies a file and reports progress in percent. Large files on Windows go through CopyFileEx
    /// with unbuffered I/O, which is considerably faster against the file server than a stream copy.
    /// </summary>
    public static void CopyWithProgress(string source, string destination, bool overwrite, IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var length = new FileInfo(source).Length;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        if (OperatingSystem.IsWindows() && length >= LargeFileThreshold)
        {
            var cancel = 0;
            var lastPercent = -1;
            NativeMethods.CopyProgressRoutine callback = (total, transferred, _, _, _, _, _, _, _) =>
            {
                if (cancellationToken.IsCancellationRequested) return NativeMethods.CopyProgressResult.PROGRESS_CANCEL;
                var percent = total == 0 ? 100 : (int)(transferred * 100 / total);
                if (percent != lastPercent) { lastPercent = percent; progress?.Report(percent); }
                return NativeMethods.CopyProgressResult.PROGRESS_CONTINUE;
            };

            var flags = NativeMethods.COPY_FILE_NO_BUFFERING | (overwrite ? 0 : NativeMethods.COPY_FILE_FAIL_IF_EXISTS);
            if (!NativeMethods.CopyFileEx(LongPath.Of(source), LongPath.Of(destination), callback, IntPtr.Zero, ref cancel, flags))
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw Win32Exceptions.ForLastError("CopyFileEx", source);
            }
            GC.KeepAlive(callback);
            return;
        }

        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan))
        using (var output = new FileStream(destination, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920))
        {
            var buffer = new byte[81920];
            long copied = 0;
            var last = -1;
            int n;
            while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.Write(buffer, 0, n);
                copied += n;
                var percent = length == 0 ? 100 : (int)(copied * 100 / length);
                if (percent != last) { last = percent; progress?.Report(percent); }
            }
            output.Flush(flushToDisk: true);
        }
        File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
    }

    /// <summary>
    /// Moves a file and does not return until it has been flushed to the target volume, so a
    /// published package is never visible half-written. Falls back to copy and delete across volumes.
    /// </summary>
    public static void MoveWriteThrough(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var flags = NativeMethods.MOVEFILE_WRITE_THROUGH | NativeMethods.MOVEFILE_COPY_ALLOWED
            | (overwrite ? NativeMethods.MOVEFILE_REPLACE_EXISTING : 0);
        if (!NativeMethods.MoveFileEx(LongPath.Of(source), LongPath.Of(destination), flags))
            throw Win32Exceptions.ForLastError("MoveFileEx", source);
    }

    /// <summary>
    /// Returns a stable identifier for the file (volume serial number and file index) that survives
    /// renames, used to recognise a drop file that was renamed after a rejection.
    /// </summary>
    public static string GetFileId(string path)
    {
        using var handle = NativeMethods.CreateFile(LongPath.Of(path), NativeMethods.GENERIC_READ,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE | NativeMethods.FILE_SHARE_DELETE,
            IntPtr.Zero, NativeMethods.OPEN_EXISTING, NativeMethods.FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
        if (handle.IsInvalid) throw Win32Exceptions.ForLastError("CreateFile", path);

        if (!NativeMethods.GetFileInformationByHandle(handle, out var info))
            throw Win32Exceptions.ForLastError("GetFileInformationByHandle", path);

        var index = ((ulong)info.nFileIndexHigh << 32) | info.nFileIndexLow;
        return $"{info.dwVolumeSerialNumber:X8}-{index:X16}";
    }
}
