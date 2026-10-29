using System;
using System.Diagnostics;

namespace Northwind.Inspections.Infrastructure.Diagnostics
{
    /// <summary>
    /// Performance counters in the "Northwind Inspections" category, watched by the operations team in Perfmon/SCOM.
    /// The category is created by "Inspections.Jobs.exe install". If it is missing the counters are not updated.
    /// </summary>
    public sealed class InspectionsCounters : IDisposable
    {
        public const string Category = "Northwind Inspections";

        public const string PhotosProcessed = "Photos processed";
        public const string PhotosPerSecond = "Photos processed/sec";
        public const string ProcessingTime = "Avg. photo processing time";
        public const string ProcessingTimeBase = "Avg. photo processing time base";
        public const string PhotoBytes = "Photo bytes received";
        public const string PhotosRejected = "Photos rejected";
        public const string ReportsQueued = "Reports queued";
        public const string ReportsSent = "Reports sent";
        public const string ReportFailures = "Report failures";

        private static readonly Lazy<InspectionsCounters> Shared = new Lazy<InspectionsCounters>(() => new InspectionsCounters());

        private readonly bool enabled;
        private readonly PerformanceCounter processed;
        private readonly PerformanceCounter perSecond;
        private readonly PerformanceCounter time;
        private readonly PerformanceCounter timeBase;
        private readonly PerformanceCounter bytes;
        private readonly PerformanceCounter rejected;
        private readonly PerformanceCounter queued;
        private readonly PerformanceCounter sent;
        private readonly PerformanceCounter failures;

        public static InspectionsCounters Instance
        {
            get { return Shared.Value; }
        }

        private InspectionsCounters()
        {
            try
            {
                enabled = PerformanceCounterCategory.Exists(Category);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Performance counters unavailable: {0}", ex.Message);
                enabled = false;
            }

            if (!enabled)
            {
                return;
            }

            processed = Open(PhotosProcessed);
            perSecond = Open(PhotosPerSecond);
            time = Open(ProcessingTime);
            timeBase = Open(ProcessingTimeBase);
            bytes = Open(PhotoBytes);
            rejected = Open(PhotosRejected);
            queued = Open(ReportsQueued);
            sent = Open(ReportsSent);
            failures = Open(ReportFailures);
        }

        public static void Install()
        {
            if (PerformanceCounterCategory.Exists(Category))
            {
                PerformanceCounterCategory.Delete(Category);
            }

            var counters = new CounterCreationDataCollection
            {
                new CounterCreationData(PhotosProcessed, "Photos stored since the process started.", PerformanceCounterType.NumberOfItems64),
                new CounterCreationData(PhotosPerSecond, "Photos stored per second.", PerformanceCounterType.RateOfCountsPerSecond32),
                new CounterCreationData(ProcessingTime, "Average time to decode, resize, watermark and store a photo.", PerformanceCounterType.AverageTimer32),
                new CounterCreationData(ProcessingTimeBase, "Base for the average processing time.", PerformanceCounterType.AverageBase),
                new CounterCreationData(PhotoBytes, "Bytes of photo uploads received.", PerformanceCounterType.NumberOfItems64),
                new CounterCreationData(PhotosRejected, "Uploads rejected as invalid images.", PerformanceCounterType.NumberOfItems32),
                new CounterCreationData(ReportsQueued, "Reports waiting in the report queue (last observed).", PerformanceCounterType.NumberOfItems32),
                new CounterCreationData(ReportsSent, "Reports e-mailed since the process started.", PerformanceCounterType.NumberOfItems32),
                new CounterCreationData(ReportFailures, "Report attempts that failed.", PerformanceCounterType.NumberOfItems32)
            };

            PerformanceCounterCategory.Create(Category, "Northwind Insurance property inspections.",
                PerformanceCounterCategoryType.SingleInstance, counters);
        }

        public void PhotoStored(long elapsedTicks, long byteCount)
        {
            if (!enabled)
            {
                return;
            }
            processed.Increment();
            perSecond.Increment();
            time.IncrementBy(elapsedTicks);
            timeBase.Increment();
            bytes.IncrementBy(byteCount);
        }

        public void PhotoRejected()
        {
            if (enabled)
            {
                rejected.Increment();
            }
        }

        public void QueueDepth(int depth)
        {
            if (enabled)
            {
                queued.RawValue = depth;
            }
        }

        public void ReportSent()
        {
            if (enabled)
            {
                sent.Increment();
            }
        }

        public void ReportFailed()
        {
            if (enabled)
            {
                failures.Increment();
            }
        }

        public void Dispose()
        {
            foreach (var c in new[] { processed, perSecond, time, timeBase, bytes, rejected, queued, sent, failures })
            {
                if (c != null)
                {
                    c.Dispose();
                }
            }
        }

        private static PerformanceCounter Open(string name)
        {
            return new PerformanceCounter(Category, name, false);
        }
    }
}
