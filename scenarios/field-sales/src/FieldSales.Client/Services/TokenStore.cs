using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Wingtip.FieldSales.Client.Services
{
    public class StoredToken
    {
        public string UserName { get; set; }
        public string AccessToken { get; set; }
        public DateTime AccessTokenExpiresUtc { get; set; }
        public string RefreshToken { get; set; }
    }

    /// <summary>
    /// Keeps the tokens between sessions so that the rep can start the application without network and sync later.
    /// The file is encrypted for the Windows user profile (DPAPI).
    /// </summary>
    public class TokenStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Wingtip.FieldSales.TokenStore.v2");
        private readonly string _path;

        public TokenStore(string folder)
        {
            _path = Path.Combine(folder, "session.dat");
        }

        public StoredToken Load()
        {
            if (!File.Exists(_path)) return null;
            try
            {
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser);
                return JsonConvert.DeserializeObject<StoredToken>(Encoding.UTF8.GetString(plain));
            }
            catch (CryptographicException)
            {
                // Profile was copied to another machine or the password was reset by the service desk.
                Clear();
                return null;
            }
        }

        public void Save(StoredToken token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            var plain = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(token));
            File.WriteAllBytes(_path, ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
        }

        public void Clear()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
