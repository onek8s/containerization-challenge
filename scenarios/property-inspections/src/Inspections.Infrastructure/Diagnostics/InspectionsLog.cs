using System;
using System.Diagnostics;

namespace Northwind.Inspections.Infrastructure.Diagnostics
{
    public enum EventId
    {
        Started = 1000,
        PhotoStored = 1100,
        PhotoDeleted = 1101,
        PhotoRejected = 2100,
        ReportQueued = 1200,
        ReportSent = 1201,
        ReportRetry = 2201,
        ReportAbandoned = 3201,
        PurgeCompleted = 1300,
        ShareFailure = 3400,
        PartnerLoginFailed = 2500,
        UnhandledError = 3999
    }

    /// <summary>
    /// Writes to the "Northwind Inspections" event log. Sources are registered by "Inspections.Jobs.exe install"
    /// because creating a source needs administrative rights.
    /// </summary>
    public static class InspectionsLog
    {
        public const string LogName = "Northwind Inspections";
        public const string ServiceSource = "Inspections Service";
        public const string PortalSource = "Inspections Portal";
        public const string JobsSource = "Inspections Jobs";

        private static string source = ServiceSource;

        public static void UseSource(string sourceName)
        {
            source = sourceName;
        }

        public static void Install()
        {
            foreach (var s in new[] { ServiceSource, PortalSource, JobsSource })
            {
                if (!EventLog.SourceExists(s))
                {
                    EventLog.CreateEventSource(new EventSourceCreationData(s, LogName));
                }
            }
            using (var log = new EventLog(LogName))
            {
                log.MaximumKilobytes = 20480;
                log.ModifyOverflowPolicy(OverflowAction.OverwriteAsNeeded, 0);
            }
        }

        public static void Info(EventId id, string message, params object[] args)
        {
            Write(EventLogEntryType.Information, id, message, args);
        }

        public static void Warning(EventId id, string message, params object[] args)
        {
            Write(EventLogEntryType.Warning, id, message, args);
        }

        public static void Error(EventId id, Exception ex, string message, params object[] args)
        {
            Write(EventLogEntryType.Error, id, Format(message, args) + Environment.NewLine + Environment.NewLine + ex, null);
        }

        private static void Write(EventLogEntryType type, EventId id, string message, object[] args)
        {
            var text = Format(message, args);
            if (text.Length > 31000)
            {
                text = text.Substring(0, 31000);
            }
            try
            {
                EventLog.WriteEntry(source, text, type, (int)id);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Event log write failed ({0}): {1}", ex.Message, text);
            }
        }

        private static string Format(string message, object[] args)
        {
            return args == null || args.Length == 0 ? message : string.Format(message, args);
        }
    }
}
