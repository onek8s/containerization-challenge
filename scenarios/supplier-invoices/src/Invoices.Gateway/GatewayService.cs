using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Wingtip.Invoices.Gateway.Channels;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Data;
using Wingtip.Invoices.Infrastructure.Diagnostics;
using Wingtip.Invoices.Infrastructure.Hosting;

namespace Wingtip.Invoices.Gateway
{
    public class GatewayService : HubServiceBase
    {
        public const string Name = "InvoiceGateway";

        private readonly FileRepository files = new FileRepository();
        private HubCounters counters;
        private Thread poller;
        private readonly Dictionary<int, int> failuresInRow = new Dictionary<int, int>();

        public GatewayService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
        }

        protected override void StartWork()
        {
            counters = new HubCounters(Name);
            poller = StartLoop("Channel poller", HubSettings.GetSeconds("PollIntervalSeconds", 60), PollAll);
        }

        protected override void StopWork()
        {
            if (poller != null && !poller.Join(TimeSpan.FromSeconds(45))) poller.Abort();
            if (counters != null) counters.Dispose();
        }

        private void PollAll()
        {
            var intake = new FileIntake(files, counters);
            foreach (var channel in files.GetActiveChannels())
            {
                if (IsStopping) return;

                IInboundChannel source;
                try
                {
                    source = Open(channel);
                }
                catch (Exception ex)
                {
                    HubLog.Error(HubLog.Events.ChannelFailed, ex, "Channel {0} of {1} is not configured correctly.", channel.ChannelId, channel.SupplierCode);
                    continue;
                }

                try
                {
                    var items = source.List();
                    foreach (var item in items)
                    {
                        if (IsStopping) return;
                        try
                        {
                            intake.Take(channel, source, item);
                        }
                        catch (System.IO.IOException ex)
                        {
                            // Still being written or locked by the supplier; picked up on the next poll.
                            HubLog.Verbose("{0} skipped: {1}", item.Name, ex.Message);
                        }
                    }
                    files.TouchChannel(channel.ChannelId, null);
                    failuresInRow.Remove(channel.ChannelId);
                }
                catch (Exception ex)
                {
                    int count;
                    failuresInRow.TryGetValue(channel.ChannelId, out count);
                    failuresInRow[channel.ChannelId] = ++count;
                    files.TouchChannel(channel.ChannelId, ex.Message);

                    // FTP servers of small suppliers are often down for a while; report the third failure in a row.
                    if (count == 3 || count % 60 == 0)
                        HubLog.Error(HubLog.Events.ChannelFailed, ex, "Polling {0} for {1} failed {2} times in a row.", source.Description, channel.SupplierCode, count);
                }
            }
        }

        private static IInboundChannel Open(InboundChannel channel)
        {
            switch (channel.ChannelType)
            {
                case "FTP":
                    var credential = new NetworkCredential(HubSettings.Require("FtpUser"), HubSettings.Require("FtpPassword"));
                    return new FtpChannel(HubSettings.Require("FtpHost"), channel.Location, channel.FilePattern, credential, HubSettings.GetBool("FtpUseTls", true));
                case "SHARE":
                    var folder = channel.Location.StartsWith(@"\\") ? channel.Location : System.IO.Path.Combine(HubSettings.Require("DropRoot"), channel.Location);
                    return new ShareChannel(folder, channel.FilePattern, HubSettings.GetSeconds("SettleSeconds", 30));
                default:
                    throw new NotSupportedException("Unknown channel type " + channel.ChannelType + ".");
            }
        }
    }
}
