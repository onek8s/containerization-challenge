using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using BatchRelease.Vault.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static BatchRelease.Vault.Interop.AuthzNativeMethods;

namespace BatchRelease.Vault;

/// <summary>Audit event IDs written to the Security log.</summary>
public enum AuditEvent : uint
{
    DecisionSigned = 4100,
    SignatureCredentialsRejected = 4101,
    DecisionDenied = 4102,
    RecordOpened = 4103,
    RecordAccessDenied = 4104,
    RecordIntegrityFailure = 4105,
    SignerEnrolled = 4106,
    SignerRevoked = 4107,
}

/// <summary>
/// Writes signature and record access events to the Windows Security log through the
/// authorization framework's application audit interface. The service account needs the
/// "Generate security audits" right and the audit source must be installed.
/// </summary>
public sealed class SecurityAudit : IDisposable
{
    private readonly ILogger<SecurityAudit> _logger;
    private readonly bool _enabled;
    private IntPtr _provider;

    public SecurityAudit(IOptions<VaultOptions> options, ILogger<SecurityAudit> logger)
    {
        _logger = logger;
        _enabled = options.Value.SecurityAuditing;
        if (!_enabled)
            return;

        // Registering the event source needs SeAuditPrivilege enabled, not just held.
        TokenPrivileges.Enable("SeAuditPrivilege");
        if (!AuthzRegisterSecurityEventSource(0, options.Value.AuditSourceName, out _provider))
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error,
                $"Cannot register security event source '{options.Value.AuditSourceName}'. Install it with 'install-audit-source' and grant the service account SeAuditPrivilege.");
        }
    }

    public bool Enabled => _enabled;

    public void Success(AuditEvent auditEvent, SecurityIdentifier? user, params string[] values) => Report(APF_AuditSuccess, auditEvent, user, values);

    public void Failure(AuditEvent auditEvent, SecurityIdentifier? user, params string[] values) => Report(APF_AuditFailure, auditEvent, user, values);

    private void Report(uint flags, AuditEvent auditEvent, SecurityIdentifier? user, string[] values)
    {
        _logger.Log(flags == APF_AuditSuccess ? LogLevel.Information : LogLevel.Warning,
            "Audit {AuditEvent} ({Result}) user {User}: {Values}", auditEvent, flags == APF_AuditSuccess ? "success" : "failure", user, string.Join(" | ", values));

        if (!_enabled)
            return;

        byte[]? sid = null;
        if (user is not null)
        {
            sid = new byte[user.BinaryLength];
            user.GetBinaryForm(sid, 0);
        }

        var strings = values.Select(v => Marshal.StringToHGlobalUni(v ?? "")).ToArray();
        var size = Marshal.SizeOf<AUDIT_PARAM>();
        var buffer = Marshal.AllocHGlobal(size * Math.Max(strings.Length, 1));
        try
        {
            for (var i = 0; i < strings.Length; i++)
            {
                Marshal.StructureToPtr(new AUDIT_PARAM { Type = AUDIT_PARAM_TYPE.APT_String, Data0 = strings[i] }, buffer + i * size, false);
            }

            var parameters = new AUDIT_PARAMS
            {
                Length = 0,
                Flags = flags,
                Count = (ushort)strings.Length,
                Parameters = buffer,
            };

            if (!AuthzReportSecurityEventFromParams(flags, _provider, (uint)auditEvent, sid, ref parameters))
            {
                // A failed audit must not go unnoticed: callers treat this as a failed operation.
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Writing security audit event {auditEvent} failed.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            foreach (var s in strings)
                Marshal.FreeHGlobal(s);
        }
    }

    /// <summary>Registers the audit source with the local security authority (run once, elevated).</summary>
    public static void Install(string sourceName, string executablePath)
    {
        var registration = new AUTHZ_SOURCE_SCHEMA_REGISTRATION
        {
            dwFlags = AUTHZ_ALLOW_MULTIPLE_SOURCE_INSTANCES,
            szEventSourceName = sourceName,
            szExecutableImagePath = executablePath,
        };
        if (!AuthzInstallSecurityEventSource(0, ref registration))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 183 && error != 5010) // ERROR_ALREADY_EXISTS, ERROR_OBJECT_ALREADY_EXISTS (source already installed)
                throw new Win32Exception(error, $"AuthzInstallSecurityEventSource failed for '{sourceName}'.");
        }
    }

    public static void Uninstall(string sourceName)
    {
        if (!AuthzUninstallSecurityEventSource(0, sourceName))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"AuthzUninstallSecurityEventSource failed for '{sourceName}'.");
    }

    public void Dispose()
    {
        if (_provider != IntPtr.Zero)
            AuthzUnregisterSecurityEventSource(0, ref _provider);
    }
}
