using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Litware.DocControl.Platform;

internal static class Win32Exceptions
{
    public static IOException ForLastError(string operation, string path)
    {
        var code = Marshal.GetLastWin32Error();
        var message = new Win32Exception(code).Message;
        return new IOException($"{operation} failed for '{path}': {message} (0x{code:X8})", code);
    }
}
