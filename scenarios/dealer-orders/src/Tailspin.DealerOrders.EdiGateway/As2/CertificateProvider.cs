using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using log4net;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.EdiGateway.As2
{
    /// <summary>
    /// Our AS2 certificate comes from LocalMachine\My by thumbprint (the service account has read access to its private key,
    /// see Import-Certificates.ps1); test systems may use a PFX file instead. Partner certificates are .cer files.
    /// </summary>
    public class CertificateProvider
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CertificateProvider));

        private readonly GatewaySettings _settings;
        private readonly ConcurrentDictionary<string, X509Certificate2> _partners = new ConcurrentDictionary<string, X509Certificate2>(StringComparer.OrdinalIgnoreCase);
        private X509Certificate2 _own;

        public CertificateProvider(GatewaySettings settings)
        {
            _settings = settings;
        }

        public X509Certificate2 Own => _own ?? (_own = LoadOwn());

        public X509Certificate2 Partner(EdiPartner partner)
        {
            if (partner == null || string.IsNullOrEmpty(partner.PartnerCertificateFile)) return null;
            var path = Path.IsPathRooted(partner.PartnerCertificateFile)
                ? partner.PartnerCertificateFile
                : Path.Combine(_settings.PartnerCertificateFolder, partner.PartnerCertificateFile);
            return _partners.GetOrAdd(path, p =>
            {
                var cert = new X509Certificate2(p);
                if (cert.NotAfter < DateTime.Now.AddDays(30))
                    Log.WarnFormat("AS2 certificate of {0} expires {1:yyyy-MM-dd} ({2})", partner.Name, cert.NotAfter, cert.Subject);
                return cert;
            });
        }

        private X509Certificate2 LoadOwn()
        {
            if (!string.IsNullOrEmpty(_settings.CertificatePfx))
                return new X509Certificate2(_settings.CertificatePfx, _settings.CertificatePfxPassword,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);

            if (string.IsNullOrEmpty(_settings.CertificateThumbprint))
                throw new InvalidOperationException("Set Edi.As2.CertificateThumbprint (LocalMachine\\My) or Edi.As2.CertificatePfx.");

            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            try
            {
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, _settings.CertificateThumbprint.Replace(" ", ""), false);
                if (found.Count == 0) throw new InvalidOperationException($"AS2 certificate {_settings.CertificateThumbprint} not found in LocalMachine\\My.");
                var cert = found[0];
                if (!cert.HasPrivateKey) throw new InvalidOperationException($"AS2 certificate {cert.Subject} has no private key.");
                if (cert.NotAfter < DateTime.Now.AddDays(30)) Log.WarnFormat("Our AS2 certificate expires {0:yyyy-MM-dd}", cert.NotAfter);
                return cert;
            }
            finally
            {
                store.Close();
            }
        }
    }
}
