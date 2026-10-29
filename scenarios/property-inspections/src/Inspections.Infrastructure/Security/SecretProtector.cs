using System;
using System.Security.Cryptography;
using System.Text;

namespace Northwind.Inspections.Infrastructure.Security
{
    /// <summary>
    /// DPAPI with the current user's key. Values can only be decrypted by the same account on the same domain,
    /// so the IIS application pool must load the user profile.
    /// </summary>
    public static class SecretProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Northwind.Inspections/2019");

        public static byte[] Protect(string plainText)
        {
            if (plainText == null)
            {
                throw new ArgumentNullException("plainText");
            }
            return ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser);
        }

        public static string Unprotect(byte[] protectedBytes)
        {
            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "The stored secret cannot be decrypted by " + Environment.UserDomainName + "\\" + Environment.UserName +
                    ". Run 'Inspections.Jobs.exe protect-secret' as this account.", ex);
            }
        }
    }
}
