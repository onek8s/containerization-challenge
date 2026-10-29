using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Management;
using VanArsdel.Integration.Management.Catalog;

namespace VanArsdel.Integration.BindingTool.Commands
{
    internal static class PartiesCommand
    {
        private class PartnerFile
        {
            public List<TradingPartner> Partners { get; set; }
        }

        public static int Run(PartiesOptions o, TextWriter output)
        {
            var file = JsonConvert.DeserializeObject<PartnerFile>(File.ReadAllText(o.Partners), new StringEnumConverter());
            var catalog = new ExplorerCatalog(new BizTalkGroup(o.Server, o.Database));
            var changed = 0;
            foreach (var partner in file.Partners ?? new List<TradingPartner>())
            {
                if (!partner.Active)
                {
                    output.WriteLine("{0,-8} skipped (inactive)", partner.PartnerId);
                    continue;
                }

                var result = catalog.SyncParty(partner, o.DryRun);
                if (result.Changed)
                {
                    changed++;
                }

                output.WriteLine("{0,-8} {1}{2}{3}", partner.PartnerId, result.Created ? "created " : string.Empty,
                    result.AddedAliases.Count == 0 ? string.Empty : "+[" + string.Join(", ", result.AddedAliases) + "] ",
                    result.RemovedAliases.Count == 0 ? string.Empty : "-[" + string.Join(", ", result.RemovedAliases) + "]");
            }

            output.WriteLine("{0} part{1} {2}", changed, changed == 1 ? "y" : "ies", o.DryRun ? "would change (dry run)" : "changed");
            return 0;
        }
    }
}
