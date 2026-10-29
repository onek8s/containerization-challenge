using System;
using System.Security.Cryptography;
using System.Text;

namespace ProductReturns.Labels.Services
{
    /// <summary>
    /// Secrets in labels.settings.json are encrypted with the machine key, so the file can be copied
    /// between installs without exposing the carrier API key. Run on the target server:
    /// ProductReturns.Labels.exe protect-secret &lt;value&gt;
    /// </summary>
    public static class SecretProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WWI.ProductReturns.Labels");

        public static string Protect(string value)
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(bytes);
        }

        public static string Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue))
            {
                return null;
            }

            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedValue), Entropy, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(bytes);
        }

        public static int Run(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ProductReturns.Labels protect-secret <value>");
                return 1;
            }

            Console.WriteLine(Protect(args[1]));
            return 0;
        }
    }
}
