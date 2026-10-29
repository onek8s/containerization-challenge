using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using BatchRelease.Core.Security;
using BatchRelease.Vault.Interop;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;
using static BatchRelease.Vault.Interop.AuthzNativeMethods;

namespace BatchRelease.Vault;

/// <summary>
/// Evaluates release policies (SDDL security descriptors) with the Windows authorization
/// framework. Group membership comes from the user's logon token, or for read access from
/// the user's SID (the service account must be allowed to read tokenGroupsGlobalAndUniversal).
/// </summary>
public sealed class AccessEvaluator : IDisposable
{
    private readonly IntPtr _resourceManager;

    public AccessEvaluator(IOptions<VaultOptions> options)
    {
        if (!AuthzInitializeResourceManager(AUTHZ_RM_FLAG_NO_AUDIT, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                options.Value.ResourceManagerName, out _resourceManager))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "AuthzInitializeResourceManager failed.");
    }

    public ReleaseRights GrantedRights(SafeAccessTokenHandle token, string policySddl)
    {
        if (!AuthzInitializeContextFromToken(0, token, _resourceManager, IntPtr.Zero, default, IntPtr.Zero, out var context))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "AuthzInitializeContextFromToken failed.");
        try
        {
            return Check(context, policySddl);
        }
        finally
        {
            AuthzFreeContext(context);
        }
    }

    public ReleaseRights GrantedRights(SecurityIdentifier sid, string policySddl)
    {
        var sidBytes = new byte[sid.BinaryLength];
        sid.GetBinaryForm(sidBytes, 0);
        if (!AuthzInitializeContextFromSid(0, sidBytes, _resourceManager, IntPtr.Zero, default, IntPtr.Zero, out var context))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"AuthzInitializeContextFromSid failed for {sid}.");
        try
        {
            return Check(context, policySddl);
        }
        finally
        {
            AuthzFreeContext(context);
        }
    }

    private static unsafe ReleaseRights Check(IntPtr context, string policySddl)
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(policySddl, 1, out var sd, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The release policy is not a valid security descriptor.");
        try
        {
            uint granted = 0;
            uint error = 0;
            var request = new AUTHZ_ACCESS_REQUEST { DesiredAccess = MAXIMUM_ALLOWED };
            var reply = new AUTHZ_ACCESS_REPLY
            {
                ResultListLength = 1,
                GrantedAccessMask = (IntPtr)(&granted),
                Error = (IntPtr)(&error),
            };

            if (!AuthzAccessCheck(0, context, ref request, IntPtr.Zero, sd, IntPtr.Zero, 0, ref reply, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "AuthzAccessCheck failed.");

            if (error == ERROR_ACCESS_DENIED)
                return ReleaseRights.None;
            if (error != ERROR_SUCCESS)
                throw new Win32Exception((int)error, "Access check returned an error.");

            return (ReleaseRights)(granted & (uint)ReleaseRights.All);
        }
        finally
        {
            LocalFree(sd);
        }
    }

    public void Dispose() => AuthzFreeResourceManager(_resourceManager);
}
