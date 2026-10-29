using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Microsoft.BizTalk.ExplorerOM;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.Management.Catalog
{
    /// <summary>
    /// IGroupCatalog over Microsoft.BizTalk.ExplorerOM. ExplorerOM is a 32-bit friendly COM-backed object model:
    /// one BtsCatalogExplorer per call, changes are applied with SaveChanges and rolled back with DiscardChanges.
    /// </summary>
    public class ExplorerCatalog : IGroupCatalog
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ExplorerCatalog));
        private readonly BizTalkGroup _group;

        public ExplorerCatalog(BizTalkGroup group)
        {
            _group = group;
        }

        public IList<ApplicationSummary> Applications(string namePrefix)
        {
            var list = new List<ApplicationSummary>();
            using (var explorer = Open())
            {
                foreach (Application app in explorer.Applications)
                {
                    if (app.IsSystem || (!string.IsNullOrEmpty(namePrefix) && !app.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    list.Add(Summarize(app));
                }
            }

            return list.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public IList<PartySummary> Parties()
        {
            var list = new List<PartySummary>();
            using (var explorer = Open())
            {
                foreach (Party party in explorer.Parties)
                {
                    var summary = new PartySummary { Name = party.Name, SendPorts = party.SendPorts == null ? 0 : party.SendPorts.Count };
                    foreach (PartyAlias alias in party.Aliases)
                    {
                        summary.Aliases.Add(new PartyAliasSummary { Name = alias.Name, Qualifier = alias.Qualifier, Value = alias.Value });
                    }

                    list.Add(summary);
                }
            }

            return list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void SetReceiveLocationEnabled(string receivePort, string location, bool enabled)
        {
            Change(explorer =>
            {
                var port = explorer.ReceivePorts[receivePort];
                if (port == null)
                {
                    throw new ArgumentException("Receive port '" + receivePort + "' does not exist.");
                }

                var rl = port.ReceiveLocations[location];
                if (rl == null)
                {
                    throw new ArgumentException("Receive location '" + location + "' does not exist on '" + receivePort + "'.");
                }

                rl.Enable = enabled;
                Log.InfoFormat("Receive location {0}/{1} {2}", receivePort, location, enabled ? "enabled" : "disabled");
            });
        }

        public void SetSendPortStarted(string sendPort, bool started)
        {
            Change(explorer =>
            {
                var port = explorer.SendPorts[sendPort];
                if (port == null)
                {
                    throw new ArgumentException("Send port '" + sendPort + "' does not exist.");
                }

                // a stopped port keeps its subscription (messages queue up); unenlisting would make them suspend
                port.Status = started ? PortStatus.Started : PortStatus.Stopped;
                Log.InfoFormat("Send port {0} {1}", sendPort, started ? "started" : "stopped");
            });
        }

        public PartySyncResult SyncParty(TradingPartner partner, bool dryRun)
        {
            var result = new PartySyncResult { Party = partner.PartnerId };
            Change(explorer =>
            {
                var party = explorer.Parties[partner.PartnerId];
                if (party == null)
                {
                    party = explorer.AddNewParty();
                    party.Name = partner.PartnerId;
                    result.Created = true;
                }

                var current = new List<PartyAliasSummary>();
                var byKey = new Dictionary<PartyAliasSummary, PartyAlias>();
                foreach (PartyAlias alias in party.Aliases)
                {
                    if (alias.IsAutoCreated)
                    {
                        continue;
                    }

                    var s = new PartyAliasSummary { Name = alias.Name, Qualifier = alias.Qualifier, Value = alias.Value };
                    current.Add(s);
                    byKey[s] = alias;
                }

                var add = new List<PartyAliasSummary>();
                var remove = new List<PartyAliasSummary>();
                PartyAliases.Diff(current, PartyAliases.Expected(partner), add, remove);

                foreach (var r in remove)
                {
                    party.RemoveAlias(byKey[r]);
                    result.RemovedAliases.Add(r.Name + ":" + r.Qualifier + ":" + r.Value);
                }

                foreach (var a in add)
                {
                    if (a.Name == PartyAliases.OrganizationAlias && !result.Created)
                    {
                        continue;
                    }

                    var alias = party.AddNewAlias();
                    alias.Name = a.Name;
                    alias.Qualifier = a.Qualifier;
                    alias.Value = a.Value;
                    result.AddedAliases.Add(a.Name + ":" + a.Qualifier + ":" + a.Value);
                }
            }, dryRun);

            return result;
        }

        private BtsCatalogExplorer Open()
        {
            return new BtsCatalogExplorer { ConnectionString = _group.ExplorerConnectionString };
        }

        private void Change(Action<BtsCatalogExplorer> change, bool dryRun = false)
        {
            using (var explorer = Open())
            {
                try
                {
                    change(explorer);
                    if (dryRun)
                    {
                        explorer.DiscardChanges();
                    }
                    else
                    {
                        explorer.SaveChanges();
                    }
                }
                catch
                {
                    explorer.DiscardChanges();
                    throw;
                }
            }
        }

        private static ApplicationSummary Summarize(Application app)
        {
            var summary = new ApplicationSummary { Name = app.Name, Description = app.Description, Status = app.Status.ToString() };

            foreach (ReceivePort port in app.ReceivePorts)
            {
                var rp = new ReceivePortSummary { Name = port.Name, TwoWay = port.IsTwoWay };
                foreach (ReceiveLocation rl in port.ReceiveLocations)
                {
                    rp.Locations.Add(new ReceiveLocationSummary
                    {
                        Name = rl.Name,
                        ReceivePort = port.Name,
                        Transport = rl.TransportType == null ? null : rl.TransportType.Name,
                        Address = rl.Address,
                        Pipeline = rl.ReceivePipeline == null ? null : rl.ReceivePipeline.FullName,
                        Handler = rl.ReceiveHandler == null ? null : rl.ReceiveHandler.Host.Name,
                        Enabled = rl.Enable,
                        ServiceWindow = rl.ServiceWindowEnabled
                    });
                }

                summary.ReceivePorts.Add(rp);
            }

            foreach (SendPort port in app.SendPorts)
            {
                var t = port.PrimaryTransport;
                summary.SendPorts.Add(new SendPortSummary
                {
                    Name = port.Name,
                    Status = port.Status.ToString(),
                    Dynamic = port.IsDynamic,
                    TwoWay = port.IsTwoWay,
                    Transport = t == null || t.TransportType == null ? null : t.TransportType.Name,
                    Address = t == null ? null : t.Address,
                    Handler = t == null || t.SendHandler == null ? null : t.SendHandler.Host.Name,
                    RetryCount = t == null ? 0 : t.RetryCount,
                    RetryIntervalMinutes = t == null ? 0 : t.RetryInterval,
                    OrderedDelivery = t != null && t.OrderedDelivery,
                    Pipeline = port.SendPipeline == null ? null : port.SendPipeline.FullName,
                    Filter = port.Filter
                });
            }

            foreach (BtsOrchestration orch in app.Orchestrations)
            {
                summary.Orchestrations.Add(new OrchestrationSummary
                {
                    FullName = orch.FullName,
                    Status = orch.Status.ToString(),
                    Host = orch.Host == null ? null : orch.Host.Name
                });
            }

            return summary;
        }
    }
}
