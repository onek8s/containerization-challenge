using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StaffOnboarding.Provisioning.Interop;

/// <summary>Enables a privilege the service account holds for the current thread token.</summary>
[SupportedOSPlatform("windows")]
internal static class Privileges
{
    public const string SeRestorePrivilege = "SeRestorePrivilege";

    private const uint SePrivilegeEnabled = 0x00000002;
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;

    public static IDisposable Enable(string privilege)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
            throw new Win32Exception();

        if (!LookupPrivilegeValue(null, privilege, out var luid))
        {
            CloseHandle(token);
            throw new Win32Exception();
        }

        var state = new TokenPrivileges { PrivilegeCount = 1, Luid = luid, Attributes = SePrivilegeEnabled };
        var previous = new TokenPrivileges();
        var size = (uint)Marshal.SizeOf<TokenPrivileges>();
        if (!AdjustTokenPrivileges(token, false, ref state, size, ref previous, out _) || Marshal.GetLastWin32Error() != 0)
        {
            var error = Marshal.GetLastWin32Error();
            CloseHandle(token);
            throw new Win32Exception(error, $"{privilege} is not held by the service account.");
        }

        return new Restore(token, previous);
    }

    private sealed class Restore : IDisposable
    {
        private readonly IntPtr _token;
        private TokenPrivileges _previous;

        public Restore(IntPtr token, TokenPrivileges previous)
        {
            _token = token;
            _previous = previous;
        }

        public void Dispose()
        {
            var ignored = new TokenPrivileges();
            AdjustTokenPrivileges(_token, false, ref _previous, (uint)Marshal.SizeOf<TokenPrivileges>(), ref ignored, out _);
            CloseHandle(_token);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public long Luid;
        public uint Attributes;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out long luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAll, ref TokenPrivileges newState,
        uint bufferLength, ref TokenPrivileges previousState, out uint returnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
