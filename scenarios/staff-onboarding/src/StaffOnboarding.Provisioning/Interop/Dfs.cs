using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StaffOnboarding.Provisioning.Interop;

[SupportedOSPlatform("windows")]
internal static class Dfs
{
    private const int DfsStorageStateActive = 0x0004;

    /// <summary>Returns \\server\share of the active DFS target for a namespace path, or null when not a DFS path.</summary>
    public static string? GetActiveTarget(string path)
    {
        var status = NetDfsGetClientInfo(path, null, null, 3, out var buffer);
        if (status == 2662 /* NERR_DfsNoSuchVolume */ || status == 2663) return null;
        if (status != 0) throw new Win32Exception(status);

        try
        {
            var info = Marshal.PtrToStructure<DfsInfo3>(buffer);
            var size = Marshal.SizeOf<DfsStorageInfo>();
            for (var i = 0; i < info.NumberOfStorages; i++)
            {
                var storage = Marshal.PtrToStructure<DfsStorageInfo>(info.Storage + i * size);
                if ((storage.State & DfsStorageStateActive) != 0)
                    return $@"\\{storage.ServerName}\{storage.ShareName}";
            }
            return null;
        }
        finally
        {
            NetApiBufferFree(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DfsInfo3
    {
        public string EntryPath;
        public string Comment;
        public int State;
        public int NumberOfStorages;
        public IntPtr Storage;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DfsStorageInfo
    {
        public int State;
        public string ServerName;
        public string ShareName;
    }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetDfsGetClientInfo(string dfsEntryPath, string? serverName, string? shareName, int level, out IntPtr buffer);

    [DllImport("netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);
}
