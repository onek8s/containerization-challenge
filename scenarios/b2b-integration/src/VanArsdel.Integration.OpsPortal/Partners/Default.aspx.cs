using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI.WebControls;
using AutoMapper;
using log4net;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Management.Catalog;
using VanArsdel.Integration.OpsPortal.Models;

namespace VanArsdel.Integration.OpsPortal.Partners
{
    public partial class DefaultPage : OpsPage
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DefaultPage));

        [Dependency]
        public PartnerRepository Partners { get; set; }

        [Dependency]
        public IGroupCatalog GroupCatalog { get; set; }

        public IEnumerable<PartnerRow> GetPartners()
        {
            var partners = Partners.LoadPartners();
            IDictionary<string, PartySummary> parties;
            try
            {
                parties = GroupCatalog.Parties().ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read BizTalk parties", ex);
                parties = null;
            }

            var rows = new List<PartnerRow>();
            foreach (var partner in partners.OrderBy(p => p.PartnerId))
            {
                var row = Mapper.Map<PartnerRow>(partner);
                row.PartyState = PartyState(partner, parties);
                rows.Add(row);
            }

            return rows;
        }

        protected void PartnerGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "SyncParty" && e.CommandName != "ToggleActive")
            {
                return;
            }

            Demand(PageAccess.Administer);
            var id = (string)e.CommandArgument;
            var partner = Partners.LoadPartners().FirstOrDefault(p => p.PartnerId == id);
            if (partner == null)
            {
                Flash("Partner " + id + " not found.", true);
            }
            else if (e.CommandName == "SyncParty")
            {
                try
                {
                    var result = GroupCatalog.SyncParty(partner, false);
                    Log.InfoFormat("Party {0} synchronized by {1}: created {2}, added [{3}], removed [{4}]", id, UserName, result.Created,
                        string.Join(", ", result.AddedAliases), string.Join(", ", result.RemovedAliases));
                    Flash(result.Changed ? "Party " + id + " updated." : "Party " + id + " was already up to date.");
                }
                catch (Exception ex)
                {
                    Log.Error("Party sync failed for " + id, ex);
                    Flash("Party sync failed: " + ex.Message, true);
                }
            }
            else
            {
                Partners.SetPartnerActive(id, !partner.Active, UserName);
                DatabaseXrefProvider.Invalidate();
                Flash(id + (partner.Active ? " deactivated." : " activated.") + " The BizTalk hosts pick the change up within 10 minutes.");
            }

            Response.Redirect(Request.RawUrl, false);
        }

        private static string PartyState(Domain.Partners.TradingPartner partner, IDictionary<string, PartySummary> parties)
        {
            if (parties == null)
            {
                return "?";
            }

            PartySummary party;
            if (!parties.TryGetValue(partner.PartnerId, out party))
            {
                return "missing";
            }

            var add = new List<PartyAliasSummary>();
            var remove = new List<PartyAliasSummary>();
            PartyAliases.Diff(party.Aliases, PartyAliases.Expected(partner), add, remove);
            return add.Count == 0 && remove.Count == 0 ? "in sync" : string.Format("{0} alias(es) differ", add.Count + remove.Count);
        }
    }
}
