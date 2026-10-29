using System;
using System.Collections.Generic;
using log4net;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Management.Wmi;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.SlaMonitor
{
    /// <summary>
    /// Gathers one MonitoringSnapshot. Each source is read on its own so that one unreachable source (WMI on a
    /// rebooting server, a BAM database in maintenance) becomes a "source unavailable" problem instead of no check.
    /// </summary>
    public class SnapshotCollector
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SnapshotCollector));
        private readonly InterchangeRepository _interchanges;
        private readonly TrackingRepository _tracking;
        private readonly BizTalkDatabaseQueries _messageBox;
        private readonly IHostInstances _hosts;

        public SnapshotCollector(InterchangeRepository interchanges, TrackingRepository tracking, BizTalkDatabaseQueries messageBox, IHostInstances hosts)
        {
            _interchanges = interchanges;
            _tracking = tracking;
            _messageBox = messageBox;
            _hosts = hosts;
        }

        public MonitoringSnapshot Collect(DateTime nowUtc, IList<string> unavailable)
        {
            var snapshot = new MonitoringSnapshot { TakenUtc = nowUtc };
            Read("interchanges awaiting acknowledgement", unavailable, () => snapshot.OutboundAwaitingAck.AddRange(_interchanges.PendingAcks()));
            Read("open orders (BAM)", unavailable, () => snapshot.OpenOrders.AddRange(_tracking.OpenOrders()));
            Read("partner activity (BAM)", unavailable, () => snapshot.Partners.AddRange(_tracking.PartnerActivity()));
            Read("suspended instances (MessageBox)", unavailable, () => snapshot.Suspended.AddRange(_messageBox.SuspendedGroups()));
            Read("host instances (WMI)", unavailable, () => snapshot.HostInstances.AddRange(_hosts.All()));
            return snapshot;
        }

        private static void Read(string what, IList<string> unavailable, Action read)
        {
            try
            {
                read();
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read " + what, ex);
                unavailable.Add(what + ": " + ex.Message);
            }
        }
    }
}
