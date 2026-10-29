using System;
using Tailspin.DealerOrders.Infrastructure;

namespace Tailspin.DealerOrders.EdiGateway
{
    /// <summary>appSettings "Edi.*" for the gateway.</summary>
    public class GatewaySettings
    {
        public string ListenUrl { get; set; }
        public string OurAs2Id { get; set; }
        public string MdnAddress { get; set; }
        public string CertificateThumbprint { get; set; }
        public string CertificatePfx { get; set; }
        public string CertificatePfxPassword { get; set; }
        public string PartnerCertificateFolder { get; set; }
        public TimeSpan OutboundInterval { get; set; }
        public TimeSpan SftpPollInterval { get; set; }
        public int MaxSendAttempts { get; set; }
        public TimeSpan HttpTimeout { get; set; }
        public string SftpKeyFile { get; set; }
        public string SftpKeyPassphrase { get; set; }
        public string SftpKnownHostsFile { get; set; }
        public string SftpArchiveFolder { get; set; }

        /// <summary>When set, SFTP folders are read and written under this local folder instead (test systems).</summary>
        public string SftpLocalRoot { get; set; }

        public static GatewaySettings FromConfig() => new GatewaySettings
        {
            ListenUrl = Settings.Get("Edi.As2.ListenUrl", "http://+:4080/"),
            OurAs2Id = Settings.Get("Edi.As2.Id", "TAILSPINMACH"),
            MdnAddress = Settings.Get("Edi.As2.MdnAddress", "edi@tailspin-machinery.example"),
            CertificateThumbprint = Settings.Get("Edi.As2.CertificateThumbprint"),
            CertificatePfx = Settings.Get("Edi.As2.CertificatePfx"),
            CertificatePfxPassword = Settings.Get("Edi.As2.CertificatePfxPassword"),
            PartnerCertificateFolder = Settings.Get("Edi.As2.PartnerCertificates", @"C:\DealerOrders\Edi\Certificates"),
            OutboundInterval = Settings.Time("Edi.OutboundInterval", TimeSpan.FromSeconds(30)),
            SftpPollInterval = Settings.Time("Edi.Sftp.PollInterval", TimeSpan.FromMinutes(5)),
            MaxSendAttempts = Settings.Int("Edi.MaxSendAttempts", 10),
            HttpTimeout = Settings.Time("Edi.As2.Timeout", TimeSpan.FromSeconds(100)),
            SftpKeyFile = Settings.Get("Edi.Sftp.KeyFile", @"C:\DealerOrders\Edi\Keys\tailspin_edi.key"),
            SftpKeyPassphrase = Settings.Get("Edi.Sftp.KeyPassphrase"),
            SftpKnownHostsFile = Settings.Get("Edi.Sftp.KnownHosts", @"C:\DealerOrders\Edi\Keys\known_hosts.txt"),
            SftpArchiveFolder = Settings.Get("Edi.Sftp.Archive", "archive"),
            SftpLocalRoot = Settings.Get("Edi.Sftp.LocalRoot")
        };
    }
}
