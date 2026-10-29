using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Southridge.Clinic.Infrastructure
{
    public static class ApiKeys
    {
        /// <summary>API keys are stored as upper case SHA-256 hex of the UTF-8 key.</summary>
        public static string Hash(string apiKey)
        {
            if (apiKey == null)
            {
                throw new ArgumentNullException("apiKey");
            }

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(apiKey.Trim()));
                var sb = new StringBuilder(64);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        public static string NewKey()
        {
            byte[] bytes = new byte[24];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }

    public static class ClinicRoles
    {
        public const string FrontDesk = "FrontDesk";
        public const string Clinical = "Clinical";
        public const string Provider = "Provider";
        public const string Billing = "Billing";
        public const string Referrals = "ReferralCoordinator";
        public const string Manager = "PracticeManager";
        public const string Administrator = "Administrator";
        public const string ServiceAccount = "ServiceAccount";
        public const string Patient = "Patient";

        public static readonly string[] StaffRoles = new[] { FrontDesk, Clinical, Provider, Billing, Referrals, Manager, Administrator };
    }
}
