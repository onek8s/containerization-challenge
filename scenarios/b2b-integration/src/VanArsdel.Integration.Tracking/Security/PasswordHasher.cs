using System;
using System.Globalization;
using System.Security.Cryptography;

namespace VanArsdel.Integration.Tracking.Security
{
    /// <summary>
    /// PBKDF2-SHA1 password hashes, stored as "PBKDF2$iterations$salt$hash" (base64). The iteration count was raised
    /// from 1000 to 10000 in 2017; older hashes still verify and are rehashed at the next successful sign-in.
    /// </summary>
    public class PasswordHasher
    {
        public const int CurrentIterations = 10000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        public string Hash(string password)
        {
            var salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            return Format(CurrentIterations, salt, Derive(password, salt, CurrentIterations));
        }

        public bool Verify(string password, string stored, out bool needsRehash)
        {
            needsRehash = false;
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
            {
                return false;
            }

            var parts = stored.Split('$');
            int iterations;
            if (parts.Length != 4 || parts[0] != "PBKDF2" || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations))
            {
                return false;
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Derive(password, salt, iterations);
            if (!FixedTimeEquals(actual, expected))
            {
                return false;
            }

            needsRehash = iterations < CurrentIterations;
            return true;
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                return kdf.GetBytes(HashBytes);
            }
        }

        private static string Format(int iterations, byte[] salt, byte[] hash)
        {
            return "PBKDF2$" + iterations.ToString(CultureInfo.InvariantCulture) + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }
    }
}
