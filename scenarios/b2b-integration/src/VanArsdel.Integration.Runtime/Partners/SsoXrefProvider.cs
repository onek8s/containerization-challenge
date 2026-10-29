using VanArsdel.Integration.Data;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Runtime.Configuration;

namespace VanArsdel.Integration.Runtime.Partners
{
    /// <summary>
    /// The cross-reference provider the maps use on the BizTalk servers (appSettings VanArsdel.XrefProvider in
    /// BTSNTSvc64.exe.config), over the application database reached through the SSO connection string.
    /// </summary>
    public class SsoXrefProvider : DatabaseXrefProvider
    {
        public SsoXrefProvider()
            : base(new PartnerRepository(IntegrationDatabases.FromSso()))
        {
        }
    }

    /// <summary>Shared partner directory for pipeline components and orchestrations.</summary>
    public static class PartnerCache
    {
        private static readonly SsoXrefProvider Provider = new SsoXrefProvider();

        public static PartnerDirectory Directory
        {
            get { return Provider.CurrentDirectory; }
        }

        public static TradingPartner Get(string partnerId)
        {
            return Directory.Get(partnerId);
        }
    }
}
