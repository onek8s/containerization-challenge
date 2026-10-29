using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BatchRelease.SigningHost.Interop;

internal static class LogonNativeMethods
{
    public const int LOGON32_LOGON_NETWORK = 3;
    public const int LOGON32_PROVIDER_DEFAULT = 0;

    public const int ERROR_LOGON_FAILURE = 1326;
    public const int ERROR_ACCOUNT_RESTRICTION = 1327;
    public const int ERROR_INVALID_LOGON_HOURS = 1328;
    public const int ERROR_PASSWORD_EXPIRED = 1330;
    public const int ERROR_ACCOUNT_DISABLED = 1331;
    public const int ERROR_LOGON_TYPE_NOT_GRANTED = 1385;
    public const int ERROR_ACCOUNT_LOCKED_OUT = 1909;
    public const int ERROR_PASSWORD_MUST_CHANGE = 1907;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool LogonUserW(
        string userName, string? domain, IntPtr password, int logonType, int logonProvider, out SafeAccessTokenHandle token);
}
