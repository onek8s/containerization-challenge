using System;
using System.Diagnostics;
using System.IO;

namespace Woodgrove.Statements.Distribution
{
    /// <summary>Application event log (source 'Woodgrove Statements') plus a daily trace file in the log folder.</summary>
    public static class Log
    {
        public const string Source = "Woodgrove Statements";
        static readonly object Sync = new object();
        static string _folder;

        public static void Initialize(string folder)
        {
            _folder = folder;
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        }

        public static void Info(string message, params object[] args)
        {
            Write("INFO", Format(message, args), null);
        }

        public static void Warn(string message, params object[] args)
        {
            var text = Format(message, args);
            Write("WARN", text, null);
            Event(text, EventLogEntryType.Warning, 2000);
        }

        public static void Error(Exception ex, string message, params object[] args)
        {
            var text = Format(message, args);
            Write("ERROR", text, ex);
            Event(text + Environment.NewLine + ex, EventLogEntryType.Error, 3000);
        }

        public static void Event(string text, EventLogEntryType type, int eventId)
        {
            try
            {
                if (EventLog.SourceExists(Source))
                    EventLog.WriteEntry(Source, text.Length > 31000 ? text.Substring(0, 31000) : text, type, eventId);
            }
            catch (Exception)
            {
                // The service account may not read the security log while checking sources; the file log still has the entry.
            }
        }

        static string Format(string message, object[] args)
        {
            return args == null || args.Length == 0 ? message : string.Format(message, args);
        }

        static void Write(string level, string text, Exception ex)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + level + " [" + System.Threading.Thread.CurrentThread.ManagedThreadId + "] " + text;
            if (ex != null) line += Environment.NewLine + ex;
            Trace.WriteLine(line);
            if (Environment.UserInteractive) Console.WriteLine(line);
            if (string.IsNullOrEmpty(_folder)) return;
            lock (Sync)
            {
                try
                {
                    File.AppendAllText(Path.Combine(_folder, "distribution-" + DateTime.Now.ToString("yyyyMMdd") + ".log"), line + Environment.NewLine);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
