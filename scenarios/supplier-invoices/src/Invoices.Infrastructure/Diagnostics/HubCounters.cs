using System;
using System.Diagnostics;

namespace Wingtip.Invoices.Infrastructure.Diagnostics
{
    /// <summary>
    /// Performance counters of the "Wingtip Supplier Invoices" category, one instance per service.
    /// The operations dashboard and the SCOM management pack read these.
    /// </summary>
    public sealed class HubCounters : IDisposable
    {
        public const string Category = "Wingtip Supplier Invoices";

        private readonly PerformanceCounter filesReceived;
        private readonly PerformanceCounter filesPerSecond;
        private readonly PerformanceCounter invoicesProcessed;
        private readonly PerformanceCounter invoicesPerSecond;
        private readonly PerformanceCounter exceptions;
        private readonly PerformanceCounter failures;
        private readonly PerformanceCounter processingTime;
        private readonly PerformanceCounter processingBase;
        private readonly PerformanceCounter invoicesPosted;
        private readonly bool enabled;

        public HubCounters(string instance)
        {
            try
            {
                if (!PerformanceCounterCategory.Exists(Category)) return;
                filesReceived = Create("Files received", instance);
                filesPerSecond = Create("Files received/sec", instance);
                invoicesProcessed = Create("Invoices processed", instance);
                invoicesPerSecond = Create("Invoices processed/sec", instance);
                exceptions = Create("Invoices with exceptions", instance);
                failures = Create("Processing failures", instance);
                processingTime = Create("Avg. processing time", instance);
                processingBase = Create("Avg. processing time base", instance);
                invoicesPosted = Create("Invoices posted", instance);
                enabled = true;
            }
            catch (Exception ex)
            {
                HubLog.Warning(HubLog.Events.Unhandled, "Performance counters are not available: {0}", ex.Message);
            }
        }

        /// <summary>Creates the counter category. Needs administrator rights; run by the install scripts.</summary>
        public static void Install()
        {
            if (PerformanceCounterCategory.Exists(Category)) PerformanceCounterCategory.Delete(Category);

            var counters = new CounterCreationDataCollection
            {
                new CounterCreationData("Files received", "Supplier files received since start.", PerformanceCounterType.NumberOfItems64),
                new CounterCreationData("Files received/sec", "Supplier files received per second.", PerformanceCounterType.RateOfCountsPerSecond32),
                new CounterCreationData("Invoices processed", "Invoices parsed and matched since start.", PerformanceCounterType.NumberOfItems64),
                new CounterCreationData("Invoices processed/sec", "Invoices processed per second.", PerformanceCounterType.RateOfCountsPerSecond32),
                new CounterCreationData("Invoices with exceptions", "Invoices that need accounts payable.", PerformanceCounterType.NumberOfItems64),
                new CounterCreationData("Processing failures", "Messages that failed and were retried.", PerformanceCounterType.NumberOfItems64),
                new CounterCreationData("Avg. processing time", "Average time to process one file.", PerformanceCounterType.AverageTimer32),
                new CounterCreationData("Avg. processing time base", "Base for the average processing time.", PerformanceCounterType.AverageBase),
                new CounterCreationData("Invoices posted", "Invoices written to ERP import files.", PerformanceCounterType.NumberOfItems64)
            };
            PerformanceCounterCategory.Create(Category, "Supplier invoice hub services.", PerformanceCounterCategoryType.MultiInstance, counters);
        }

        public void FileReceived()
        {
            if (!enabled) return;
            filesReceived.Increment();
            filesPerSecond.Increment();
        }

        public void InvoiceProcessed(bool exception)
        {
            if (!enabled) return;
            invoicesProcessed.Increment();
            invoicesPerSecond.Increment();
            if (exception) exceptions.Increment();
        }

        public void Failure()
        {
            if (enabled) failures.Increment();
        }

        public void Posted(int count)
        {
            if (enabled) invoicesPosted.IncrementBy(count);
        }

        /// <summary>Records elapsed Stopwatch ticks.</summary>
        public void ProcessingTime(long stopwatchTicks)
        {
            if (!enabled) return;
            processingTime.IncrementBy(stopwatchTicks);
            processingBase.Increment();
        }

        public void Dispose()
        {
            foreach (var c in new[] { filesReceived, filesPerSecond, invoicesProcessed, invoicesPerSecond, exceptions, failures, processingTime, processingBase, invoicesPosted })
            {
                if (c == null) continue;
                c.RemoveInstance();
                c.Dispose();
            }
        }

        private static PerformanceCounter Create(string name, string instance)
        {
            return new PerformanceCounter(Category, name, instance, false) { RawValue = 0 };
        }
    }
}
