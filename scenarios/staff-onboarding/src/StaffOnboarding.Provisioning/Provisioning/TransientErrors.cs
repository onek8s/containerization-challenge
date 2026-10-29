using System.Runtime.InteropServices;

namespace StaffOnboarding.Provisioning.Provisioning;

/// <summary>Errors after which the workflow should try the same step again later.</summary>
public static class TransientErrors
{
    private static readonly HashSet<int> HResults = new()
    {
        unchecked((int)0x8007203A), // The server is not operational
        unchecked((int)0x8007200E), // The directory service is busy
        unchecked((int)0x80072029), // A decoding error / connection dropped by the server
        unchecked((int)0x80070035), // The network path was not found
        unchecked((int)0x80070040), // The specified network name is no longer available
        unchecked((int)0x800704CF), // The network location cannot be reached
        unchecked((int)0x80070079), // The semaphore timeout period has expired
        unchecked((int)0x80070020)  // The file is being used by another process
    };

    public static bool IsTransient(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is TimeoutException) return true;
            if (e.GetType().Name is "PrincipalServerDownException" or "ActiveDirectoryServerDownException") return true;
            if ((e is COMException || e is IOException || e is ExternalException) && HResults.Contains(e.HResult)) return true;
        }
        return false;
    }
}
