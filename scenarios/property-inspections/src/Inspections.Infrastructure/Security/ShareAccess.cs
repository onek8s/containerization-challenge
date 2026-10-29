using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Northwind.Inspections.Infrastructure.Configuration;

namespace Northwind.Inspections.Infrastructure.Security
{
    /// <summary>
    /// Runs file operations on the photo share as the configured share account (LOGON32_LOGON_NEW_CREDENTIALS, so
    /// only outbound network access uses those credentials). Without a share account the process identity is used.
    /// </summary>
    public sealed class ShareAccess
    {
        private const int Logon32LogonNewCredentials = 9;
        private const int Logon32ProviderWinnt50 = 3;

        private readonly string user;
        private readonly Func<string> password;

        public ShareAccess(InspectionSettings settings)
            : this(settings.ShareUser, () => settings.SharePassword)
        {
        }

        public ShareAccess(string user, Func<string> password)
        {
            this.user = user;
            this.password = password;
        }

        public void Run(Action action)
        {
            Run<object>(() =>
            {
                action();
                return null;
            });
        }

        public T Run<T>(Func<T> func)
        {
            if (string.IsNullOrEmpty(user))
            {
                return func();
            }

            string domain = ".";
            string name = user;
            var slash = user.IndexOf('\\');
            if (slash > 0)
            {
                domain = user.Substring(0, slash);
                name = user.Substring(slash + 1);
            }

            var secret = password();
            if (secret == null)
            {
                throw new InvalidOperationException("No share password is stored for this account. Run 'Inspections.Jobs.exe protect-secret SharePassword'.");
            }

            SafeAccessTokenHandle token;
            if (!LogonUser(name, domain, secret, Logon32LogonNewCredentials, Logon32ProviderWinnt50, out token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "LogonUser failed for share account " + user + ".");
            }

            using (token)
            {
                return WindowsIdentity.RunImpersonated(token, func);
            }
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword,
            int dwLogonType, int dwLogonProvider, out SafeAccessTokenHandle phToken);
    }
}
