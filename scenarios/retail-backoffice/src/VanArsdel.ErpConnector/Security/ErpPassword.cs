using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

// The connector only runs on the app03 Windows servers; DPAPI is fine there.
#pragma warning disable CA1416

namespace VanArsdel.ErpConnector.Security
{
    /// <summary>
    /// The ERP integration user's password is kept in a DPAPI-protected file (machine scope) so that it is
    /// not in appsettings. Created on the server with "VanArsdel.ErpConnector.exe --protect-password".
    /// Developers set Erp:Password (user secrets) instead.
    /// </summary>
    public static class ErpPassword
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VanArsdel.ErpConnector");

        public static string Resolve(IConfiguration configuration)
        {
            var plain = configuration["Erp:Password"];
            if (!string.IsNullOrEmpty(plain))
            {
                return plain;
            }
            var file = configuration["Erp:PasswordFile"];
            if (string.IsNullOrEmpty(file))
            {
                throw new InvalidOperationException("Set Erp:PasswordFile (DPAPI) or Erp:Password.");
            }
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(file), Entropy, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(bytes);
        }

        public static void Protect(string password, string file)
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.LocalMachine);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
            File.WriteAllBytes(file, bytes);
        }
    }
}
