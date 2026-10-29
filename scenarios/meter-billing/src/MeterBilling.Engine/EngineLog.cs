using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Coho.Billing.Engine
{
    /// <summary>Writes to the Application event log (source 'Coho Meter Billing') and to a daily log file read by Billing Monitor.</summary>
    public static class EngineLog
    {
        public const string EventSource = "Coho Meter Billing";
        private static readonly object Sync = new object();
        private static string _folder;
        private static bool _console;

        public static void Initialize(string folder, bool console)
        {
            _folder = folder;
            _console = console;
            Directory.CreateDirectory(folder);
        }

        public static void Info(string message, params object[] args)
        {
            Write(EventLogEntryType.Information, 1000, message, args);
        }

        public static void Warning(string message, params object[] args)
        {
            Write(EventLogEntryType.Warning, 2000, message, args);
        }

        public static void Error(Exception exception, string message, params object[] args)
        {
            Write(EventLogEntryType.Error, 3000, (args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, message, args) : message) + Environment.NewLine + exception);
        }

        public static void Event(EventLogEntryType type, int eventId, string message)
        {
            Write(type, eventId, message);
        }

        public static void InstallSource()
        {
            if (!EventLog.SourceExists(EventSource))
            {
                EventLog.CreateEventSource(EventSource, "Application");
            }
        }

        private static void Write(EventLogEntryType type, int eventId, string message, params object[] args)
        {
            var text = args != null && args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, message, args) : message;
            var level = type == EventLogEntryType.Error ? "ERROR" : type == EventLogEntryType.Warning ? "WARN " : "INFO ";
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + level + " " + text;

            lock (Sync)
            {
                if (_console)
                {
                    Console.WriteLine(line);
                }

                if (_folder != null)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(_folder, "engine-" + DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log"), line + Environment.NewLine);
                    }
                    catch (IOException)
                    {
                        // Monitor may hold the file briefly; the event log still has the entry.
                    }
                }
            }

            if (type != EventLogEntryType.Information || eventId != 1000)
            {
                try
                {
                    EventLog.WriteEntry(EventSource, text.Length > 30000 ? text.Substring(0, 30000) : text, type, eventId);
                }
                catch (Exception)
                {
                    // Source not installed (run 'MeterBilling.Engine.exe install-eventlog' as administrator).
                }
            }
        }
    }
}
