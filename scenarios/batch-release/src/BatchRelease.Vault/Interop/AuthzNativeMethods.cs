using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BatchRelease.Vault.Interop;

internal static class AuthzNativeMethods
{
    public const uint AUTHZ_RM_FLAG_NO_AUDIT = 0x1;
    public const uint MAXIMUM_ALLOWED = 0x02000000;
    public const uint APF_AuditFailure = 0x0;
    public const uint APF_AuditSuccess = 0x1;
    public const uint AUTHZ_ALLOW_MULTIPLE_SOURCE_INSTANCES = 0x1;
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_ACCESS_DENIED = 5;

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AUTHZ_ACCESS_REQUEST
    {
        public uint DesiredAccess;
        public IntPtr PrincipalSelfSid;
        public IntPtr ObjectTypeList;
        public uint ObjectTypeListLength;
        public IntPtr OptionalArguments;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AUTHZ_ACCESS_REPLY
    {
        public uint ResultListLength;
        public IntPtr GrantedAccessMask;
        public IntPtr SaclEvaluationResults;
        public IntPtr Error;
    }

    public enum AUDIT_PARAM_TYPE
    {
        APT_None = 1,
        APT_String = 2,
        APT_Ulong = 3,
        APT_Pointer = 4,
        APT_Sid = 5,
        APT_LogonId = 6,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AUDIT_PARAM
    {
        public AUDIT_PARAM_TYPE Type;
        public uint Length;
        public uint Flags;
        public IntPtr Data0;
        public IntPtr Data1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AUDIT_PARAMS
    {
        public uint Length;
        public uint Flags;
        public ushort Count;
        public IntPtr Parameters;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct AUTHZ_SOURCE_SCHEMA_REGISTRATION
    {
        public uint dwFlags;
        public string szEventSourceName;
        public string? szEventMessageFile;
        public string? szEventSourceXmlSchemaFile;
        public string? szEventAccessStringsFile;
        public string? szExecutableImagePath;
        public IntPtr pProviderGuid;
        public uint dwObjectTypeNameCount;
        public IntPtr ObjectTypeNames;
    }

    [DllImport("authz.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool AuthzInitializeResourceManager(
        uint flags, IntPtr pfnAccessCheck, IntPtr pfnComputeDynamicGroups, IntPtr pfnFreeDynamicGroups,
        string resourceManagerName, out IntPtr resourceManager);

    [DllImport("authz.dll", SetLastError = true)]
    public static extern bool AuthzFreeResourceManager(IntPtr resourceManager);

    [DllImport("authz.dll", SetLastError = true)]
    public static extern bool AuthzInitializeContextFromToken(
        uint flags, SafeAccessTokenHandle tokenHandle, IntPtr resourceManager, IntPtr expirationTime,
        LUID identifier, IntPtr dynamicGroupArgs, out IntPtr clientContext);

    [DllImport("authz.dll", SetLastError = true)]
    public static extern bool AuthzInitializeContextFromSid(
        uint flags, byte[] userSid, IntPtr resourceManager, IntPtr expirationTime,
        LUID identifier, IntPtr dynamicGroupArgs, out IntPtr clientContext);

    [DllImport("authz.dll", SetLastError = true)]
    public static extern bool AuthzFreeContext(IntPtr clientContext);

    [DllImport("authz.dll", SetLastError = true)]
    public static extern bool AuthzAccessCheck(
        uint flags, IntPtr clientContext, ref AUTHZ_ACCESS_REQUEST request, IntPtr auditEvent,
        IntPtr securityDescriptor, IntPtr optionalSecurityDescriptorArray, uint optionalSecurityDescriptorCount,
        ref AUTHZ_ACCESS_REPLY reply, IntPtr accessCheckResults);

    [DllImport("authz.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool AuthzRegisterSecurityEventSource(uint flags, string eventSourceName, out IntPtr eventProvider);

    [DllImport("authz.dll", SetLastError = true)]
    public static extern bool AuthzUnregisterSecurityEventSource(uint flags, ref IntPtr eventProvider);

    [DllImport("authz.dll", SetLastError = true)]
    public static extern bool AuthzReportSecurityEventFromParams(
        uint flags, IntPtr eventProvider, uint auditId, byte[]? userSid, ref AUDIT_PARAMS auditParams);

    [DllImport("authz.dll", SetLastError = true)]
    public static extern bool AuthzInstallSecurityEventSource(uint flags, ref AUTHZ_SOURCE_SCHEMA_REGISTRATION registration);

    [DllImport("authz.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool AuthzUninstallSecurityEventSource(uint flags, string eventSourceName);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor, uint stringSdRevision, out IntPtr securityDescriptor, out uint securityDescriptorSize);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr hMem);
}
