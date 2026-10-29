using System;
using System.Security.Cryptography.X509Certificates;

namespace Lucerne.ProjectSites.SharePoint.Auth
{
    public static class SigningCertificate
    {
        /// <summary>
        /// Loads the S2S signing certificate: by thumbprint from LocalMachine\My (production), or from a PFX file
        /// (development and the lab simulators).
        /// </summary>
        public static X509Certificate2 Load(string thumbprint, string pfxPath, string pfxPassword)
        {
            if (!string.IsNullOrEmpty(pfxPath))
            {
                return new X509Certificate2(pfxPath, pfxPassword, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
            }

            if (string.IsNullOrEmpty(thumbprint))
            {
                throw new InvalidOperationException("Neither a certificate thumbprint nor a PFX file is configured.");
            }

            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint.Replace(" ", string.Empty), false);
                if (found.Count == 0)
                {
                    throw new InvalidOperationException("Certificate " + thumbprint + " not found in LocalMachine\\My.");
                }

                return found[0];
            }
        }
    }
}
