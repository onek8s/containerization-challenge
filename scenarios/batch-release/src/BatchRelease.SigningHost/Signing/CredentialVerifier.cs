using System.Runtime.InteropServices;
using System.Security.Principal;
using BatchRelease.SigningHost.Interop;
using Microsoft.Extensions.Options;
using Microsoft.Win32.SafeHandles;
using static BatchRelease.SigningHost.Interop.LogonNativeMethods;

namespace BatchRelease.SigningHost.Signing;

public sealed class SignerLogon : IDisposable
{
    public SignerLogon(SafeAccessTokenHandle token, SecurityIdentifier sid, string accountName)
    {
        Token = token;
        Sid = sid;
        AccountName = accountName;
    }

    public SafeAccessTokenHandle Token { get; }
    public SecurityIdentifier Sid { get; }
    public string AccountName { get; }

    public void Dispose() => Token.Dispose();
}

public sealed record LogonFailure(int Win32Error, string Reason);

/// <summary>
/// Checks the signer's account name and password with a network logon. The resulting token
/// is used for the access check and, impersonated, to open the signer's key.
/// </summary>
public sealed class CredentialVerifier
{
    private readonly SigningOptions _options;

    public CredentialVerifier(IOptions<SigningOptions> options) => _options = options.Value;

    public (string Domain, string User) Split(string accountName)
    {
        var name = accountName.Trim();
        var slash = name.IndexOf('\\');
        if (slash > 0)
            return (name[..slash], name[(slash + 1)..]);
        if (name.Contains('@'))
            return ("", name);
        return (string.IsNullOrEmpty(_options.DefaultDomain) ? Environment.MachineName : _options.DefaultDomain, name);
    }

    public SignerLogon? Logon(string accountName, string password, out LogonFailure? failure)
    {
        failure = null;
        var (domain, user) = Split(accountName);

        var secret = Marshal.StringToHGlobalUni(password);
        try
        {
            if (!LogonUserW(user, string.IsNullOrEmpty(domain) ? null : domain, secret, LOGON32_LOGON_NETWORK, LOGON32_PROVIDER_DEFAULT, out var token))
            {
                var error = Marshal.GetLastWin32Error();
                failure = new LogonFailure(error, Describe(error));
                return null;
            }

            using var identity = new WindowsIdentity(token.DangerousGetHandle());
            return new SignerLogon(token, identity.User!, identity.Name);
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(secret);
        }
    }

    private static string Describe(int error) => error switch
    {
        ERROR_LOGON_FAILURE => "Unknown account or wrong password.",
        ERROR_ACCOUNT_LOCKED_OUT => "The account is locked out.",
        ERROR_ACCOUNT_DISABLED => "The account is disabled.",
        ERROR_PASSWORD_EXPIRED or ERROR_PASSWORD_MUST_CHANGE => "The password has expired.",
        ERROR_ACCOUNT_RESTRICTION or ERROR_INVALID_LOGON_HOURS => "Account restrictions prevent signing now.",
        ERROR_LOGON_TYPE_NOT_GRANTED => "The account is not allowed to log on to the signing host from the network.",
        _ => $"Logon failed (error {error}).",
    };
}
