using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Threading;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Data;
using Wingtip.Invoices.Infrastructure.Diagnostics;
using Wingtip.Invoices.Infrastructure.Hosting;

namespace Wingtip.Invoices.ErpLink
{
    public class ErpLinkService : HubServiceBase
    {
        public const string Name = "InvoiceErpLink";

        private HubCounters counters;
        private Thread receiver;
        private Thread writer;
        private Thread acknowledgements;

        public ErpLinkService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
        }

        protected override void StartWork()
        {
            counters = new HubCounters(Name);

            // Channel (tcp 8095, Windows authentication) and the ErpLookup.rem singleton are in the .config file.
            RemotingConfiguration.Configure(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile, true);

            var postings = new PostingRepository();
            var postingReceiver = new PostingReceiver(postings, Stopping);
            receiver = new Thread(postingReceiver.Run) { Name = "Posting receiver", IsBackground = true };
            receiver.Start();

            var batchWriter = new BatchWriter(postings, counters);
            writer = StartLoop("Batch writer", TimeSpan.FromMinutes(1), batchWriter.Run);

            var reader = new AcknowledgementReader(postings);
            acknowledgements = StartLoop("Acknowledgement reader", HubSettings.GetSeconds("AckPollSeconds", 120), reader.Run);
        }

        protected override void StopWork()
        {
            foreach (var channel in ChannelServices.RegisteredChannels)
            {
                ChannelServices.UnregisterChannel(channel);
            }
            foreach (var t in new[] { receiver, writer, acknowledgements })
            {
                if (t != null) t.Join(TimeSpan.FromSeconds(30));
            }
            if (counters != null) counters.Dispose();
        }
    }
}
