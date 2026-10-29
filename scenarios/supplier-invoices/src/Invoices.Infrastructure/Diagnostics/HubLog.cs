using System;
using System.Diagnostics;

namespace Wingtip.Invoices.Infrastructure.Diagnostics
{
    /// <summary>
    /// Writes to the "Wingtip Invoices" event log and to the trace listeners in the .config file.
    /// Operations monitors the event log for the event ids below.
    /// </summary>
    public static class HubLog
    {
        public const string LogName = "Wingtip Invoices";

        public static class Events
        {
            public const int Started = 1000;
            public const int Stopped = 1001;
            public const int FileReceived = 2000;
            public const int FileDuplicate = 2001;
            public const int ChannelFailed = 2100;
            public const int InvoiceProcessed = 3000;
            public const int FileRejected = 3100;
            public const int ProcessingFailed = 3200;
            public const int MovedToPoison = 3300;
            public const int ErpUnavailable = 3400;
            public const int BatchWritten = 4000;
            public const int AckProcessed = 4100;
            public const int PostingRejected = 4200;
            public const int PostingFailed = 4300;
            public const int ControlCall = 5000;
            public const int ControlDenied = 5100;
            public const int Unhandled = 9000;
        }

        private static readonly TraceSource Trace = new TraceSource("Wingtip.Invoices", SourceLevels.Information);
        private static string source = "InvoiceHub";

        public static void Initialize(string sourceName)
        {
            source = sourceName;
        }

        /// <summary>Creates the event source. Needs administrator rights; run by the install scripts.</summary>
        public static void Install(string sourceName)
        {
            if (!EventLog.SourceExists(sourceName))
            {
                EventLog.CreateEventSource(new EventSourceCreationData(sourceName, LogName));
            }
            using (var log = new EventLog(LogName))
            {
                log.MaximumKilobytes = 20480;
                log.ModifyOverflowPolicy(OverflowAction.OverwriteAsNeeded, 0);
            }
        }

        public static void Info(int eventId, string message, params object[] args)
        {
            Write(EventLogEntryType.Information, TraceEventType.Information, eventId, message, args);
        }

        public static void Warning(int eventId, string message, params object[] args)
        {
            Write(EventLogEntryType.Warning, TraceEventType.Warning, eventId, message, args);
        }

        public static void Error(int eventId, Exception ex, string message, params object[] args)
        {
            var text = args.Length == 0 ? message : string.Format(message, args);
            Write(EventLogEntryType.Error, TraceEventType.Error, eventId, text + Environment.NewLine + ex, new object[0]);
        }

        public static void Verbose(string message, params object[] args)
        {
            Trace.TraceEvent(TraceEventType.Verbose, 0, args.Length == 0 ? message : string.Format(message, args));
        }

        private static void Write(EventLogEntryType type, TraceEventType traceType, int eventId, string message, object[] args)
        {
            var text = args.Length == 0 ? message : string.Format(message, args);
            Trace.TraceEvent(traceType, eventId, text);
            Trace.Flush();

            try
            {
                if (text.Length > 31000) text = text.Substring(0, 31000);
                EventLog.WriteEntry(source, text, type, eventId);
            }
            catch (Exception)
            {
                // Event source not registered (developer machine). The trace file still has the entry.
            }
        }
    }
}
