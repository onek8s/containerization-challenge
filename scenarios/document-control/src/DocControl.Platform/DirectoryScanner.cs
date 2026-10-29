using Litware.DocControl.Platform.Interop;

namespace Litware.DocControl.Platform;

public sealed record ScannedFile(string FullPath, string Name, long Length, DateTime LastWriteUtc, FileAttributes Attributes);

/// <summary>
/// Lists files with FindFirstFileEx (basic info level, large fetch). On the drop share this is much
/// quicker than DirectoryInfo for folders with thousands of files, because no short names are
/// generated and the server returns bigger batches.
/// </summary>
public static class DirectoryScanner
{
    public static IEnumerable<ScannedFile> EnumerateFiles(string directory, string pattern = "*", bool recursive = false)
    {
        var pending = new Stack<string>();
        pending.Push(directory);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            var search = Path.Combine(LongPath.Of(current), pattern == "*" || !recursive ? pattern : "*");

            using var handle = NativeMethods.FindFirstFileEx(search, NativeMethods.FINDEX_INFO_LEVELS.FindExInfoBasic,
                out var data, NativeMethods.FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero,
                NativeMethods.FIND_FIRST_EX_LARGE_FETCH);

            if (handle.IsInvalid)
            {
                var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (error == NativeMethods.ERROR_FILE_NOT_FOUND) continue;
                if (error == NativeMethods.ERROR_PATH_NOT_FOUND) throw new DirectoryNotFoundException($"Folder '{current}' was not found.");
                throw Win32Exceptions.ForLastError("FindFirstFileEx", current);
            }

            do
            {
                if (data.cFileName is "." or "..") continue;
                var full = Path.Combine(current, data.cFileName);

                if ((data.dwFileAttributes & FileAttributes.Directory) != 0)
                {
                    if (recursive) pending.Push(full);
                    continue;
                }

                if (recursive && pattern != "*" && !System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, data.cFileName))
                    continue;

                yield return new ScannedFile(full, data.cFileName,
                    ((long)data.nFileSizeHigh << 32) | data.nFileSizeLow,
                    data.ftLastWriteTime.ToDateTimeUtc(),
                    data.dwFileAttributes);
            }
            while (NativeMethods.FindNextFile(handle, out data));
        }
    }
}
