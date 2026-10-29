using System;
using System.Configuration;
using Lucerne.ProjectSites.SharePoint.Auth;

namespace Lucerne.ProjectSites.Dashboards.Infrastructure
{
    /// <summary>appSettings of the add-in web (ClientId and IssuerId as registered with Register-HighTrustAddIn).</summary>
    public class AddInSettings
    {
        public string ClientId { get; set; }
        public string IssuerId { get; set; }
        public string Realm { get; set; }
        public string SigningCertificateThumbprint { get; set; }
        public string SigningCertificatePath { get; set; }
        public string SigningCertificatePassword { get; set; }
        public string HubUrl { get; set; }

        public static AddInSettings FromConfig()
        {
            var a = ConfigurationManager.AppSettings;
            return new AddInSettings
            {
                ClientId = a["ClientId"],
                IssuerId = a["IssuerId"],
                Realm = a["Realm"],
                SigningCertificateThumbprint = a["ClientSigningCertificateThumbprint"],
                SigningCertificatePath = a["ClientSigningCertificatePath"],
                SigningCertificatePassword = a["ClientSigningCertificatePassword"],
                HubUrl = a["HubUrl"]
            };
        }

        public HighTrustTokenIssuer CreateIssuer()
        {
            var realm = string.IsNullOrEmpty(Realm) ? new RealmDiscovery().GetRealm(new Uri(HubUrl)) : Realm;
            var certificate = SigningCertificate.Load(SigningCertificateThumbprint, SigningCertificatePath, SigningCertificatePassword);
            return new HighTrustTokenIssuer(certificate, IssuerId, ClientId, realm);
        }
    }
}
