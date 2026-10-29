using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
using Wingtip.Invoices.Infrastructure.Configuration;
using Wingtip.Invoices.Infrastructure.Diagnostics;
using Wingtip.Invoices.Infrastructure.Hosting;
using Wingtip.Invoices.Processor.Control;

namespace Wingtip.Invoices.Processor
{
    public class ProcessorService : HubServiceBase
    {
        public const string Name = "InvoiceProcessor";

        private readonly List<Thread> workers = new List<Thread>();
        private HubCounters counters;
        private ServiceHost controlHost;

        public ProcessorService()
        {
            ServiceName = Name;
            CanStop = true;
            CanShutdown = true;
        }

        protected override void StartWork()
        {
            counters = new HubCounters(Name);
            var erp = new ErpLookupClient();
            var pipeline = new InvoicePipeline(erp, counters);

            try
            {
                HubLog.Info(HubLog.Events.Started, "ERP lookup: {0}", erp.Ping());
            }
            catch (ErpUnavailableException ex)
            {
                HubLog.Warning(HubLog.Events.ErpUnavailable, "ERP lookup is not available at start; files wait in the queue. {0}", ex.Message);
            }

            ProcessorControl.Pipeline = pipeline;
            controlHost = new ServiceHost(typeof(ProcessorControl));
            controlHost.Open();

            var count = HubSettings.GetInt("Workers", 2);
            for (var i = 1; i <= count; i++)
            {
                var worker = new QueueWorker(pipeline, counters, Stopping);
                var thread = new Thread(worker.Run) { Name = "Queue worker " + i, IsBackground = true };
                thread.Start();
                workers.Add(thread);
            }
        }

        protected override void StopWork()
        {
            if (controlHost != null)
            {
                try
                {
                    controlHost.Close(TimeSpan.FromSeconds(10));
                }
                catch (Exception)
                {
                    controlHost.Abort();
                }
            }

            foreach (var t in workers)
            {
                t.Join(TimeSpan.FromSeconds(30));
            }
            workers.Clear();
            if (counters != null) counters.Dispose();
        }
    }
}
