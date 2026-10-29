using System.Runtime.InteropServices;
using System.Text;
using PolicyChanges.DeskRunner.Native;

namespace PolicyChanges.DeskRunner.Desktop;

public sealed record StoredCredential(string UserName, string Password);

/// <summary>PolicyDesk sign-in stored in Windows Credential Manager of the desk user.</summary>
internal static class CredentialStore
{
    public static StoredCredential? Read(string target)
    {
        if (!Advapi32.CredRead(target, Advapi32.CRED_TYPE_GENERIC, 0, out var ptr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == Advapi32.ERROR_NOT_FOUND) return null;
            throw new InvalidOperationException($"CredRead failed with error {error}.");
        }
        try
        {
            var cred = Marshal.PtrToStructure<Advapi32.CREDENTIAL>(ptr);
            var password = cred.CredentialBlobSize == 0
                ? ""
                : Marshal.PtrToStringUni(cred.CredentialBlob, cred.CredentialBlobSize / 2);
            return new StoredCredential(cred.UserName, password);
        }
        finally
        {
            Advapi32.CredFree(ptr);
        }
    }

    public static void Write(string target, string userName, string password)
    {
        var blob = Encoding.Unicode.GetBytes(password);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);
            var cred = new Advapi32.CREDENTIAL
            {
                Type = Advapi32.CRED_TYPE_GENERIC,
                TargetName = target,
                UserName = userName,
                CredentialBlob = blobPtr,
                CredentialBlobSize = blob.Length,
                Persist = Advapi32.CRED_PERSIST_LOCAL_MACHINE,
                Comment = "PolicyDesk sign-in for Policy Changes desk runner",
            };
            if (!Advapi32.CredWrite(ref cred, 0))
                throw new InvalidOperationException($"CredWrite failed with error {Marshal.GetLastWin32Error()}.");
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }
}
