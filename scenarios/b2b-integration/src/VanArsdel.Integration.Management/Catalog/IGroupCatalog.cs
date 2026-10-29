using System.Collections.Generic;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.Management.Catalog
{
    /// <summary>Applications, ports and parties of the BizTalk group (ExplorerOM on the servers).</summary>
    public interface IGroupCatalog
    {
        IList<ApplicationSummary> Applications(string namePrefix);

        IList<PartySummary> Parties();

        void SetReceiveLocationEnabled(string receivePort, string location, bool enabled);

        void SetSendPortStarted(string sendPort, bool started);

        PartySyncResult SyncParty(TradingPartner partner, bool dryRun);
    }
}
