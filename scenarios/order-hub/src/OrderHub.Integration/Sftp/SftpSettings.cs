using Litware.OrderHub.Integration.Configuration;

namespace Litware.OrderHub.Integration.Sftp
{
    public class SftpSettings
    {
        public string Host { get; set; }
        public int Port { get; set; } = 22;
        public string Username { get; set; }
        public string Password { get; set; }
        public string PrivateKeyFile { get; set; }
        public string PrivateKeyPassphrase { get; set; }

        /// <summary>MD5 fingerprint of the server host key (aa:bb:...). Empty trusts any key.</summary>
        public string HostKeyFingerprint { get; set; }

        public static SftpSettings Read(SettingsReader settings, string prefix)
        {
            return new SftpSettings
            {
                Host = settings.Require(prefix + ".Host"),
                Port = settings.GetInt(prefix + ".Port", 22),
                Username = settings.Require(prefix + ".Username"),
                Password = settings.Get(prefix + ".Password"),
                PrivateKeyFile = settings.Get(prefix + ".PrivateKeyFile"),
                PrivateKeyPassphrase = settings.Get(prefix + ".PrivateKeyPassphrase"),
                HostKeyFingerprint = settings.Get(prefix + ".HostKeyFingerprint")
            };
        }

        public override string ToString()
        {
            return Username + "@" + Host + ":" + Port;
        }
    }
}
