using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Practices.EnterpriseLibrary.Logging;

namespace Southridge.Clinic.Infrastructure
{
    public static class LogCategory
    {
        public const string General = "General";
        public const string Audit = "Audit";
        public const string Scheduling = "Scheduling";
        public const string Billing = "Billing";
        public const string Interface = "Interface";
        public const string Security = "Security";
        public const string Performance = "Performance";
    }

    /// <summary>
    /// Thin wrapper over the Enterprise Library Logging Application Block. Listeners and categories are set in the
    /// loggingConfiguration section of each host (rolling flat files under the log folder by default).
    /// </summary>
    public static class ClinicLog
    {
        public static void Info(string category, string message)
        {
            Write(category, message, TraceEventType.Information, null);
        }

        public static void Info(string category, string format, params object[] args)
        {
            Write(category, string.Format(format, args), TraceEventType.Information, null);
        }

        public static void Warning(string category, string message)
        {
            Write(category, message, TraceEventType.Warning, null);
        }

        public static void Error(string category, string message, Exception ex)
        {
            var properties = new Dictionary<string, object>();
            if (ex != null)
            {
                properties.Add("Exception", ex.ToString());
            }

            Write(category, message, TraceEventType.Error, properties);
        }

        public static void Audit(string userName, string action, string details)
        {
            var properties = new Dictionary<string, object>();
            properties.Add("User", userName);
            properties.Add("Action", action);
            Write(LogCategory.Audit, details, TraceEventType.Information, properties);
        }

        private static void Write(string category, string message, TraceEventType severity, IDictionary<string, object> properties)
        {
            try
            {
                var entry = new LogEntry
                {
                    Message = message,
                    Severity = severity,
                    Priority = severity == TraceEventType.Error ? 10 : 5,
                    Title = category
                };
                entry.Categories.Add(category);
                if (properties != null)
                {
                    entry.ExtendedProperties = properties;
                }

                Logger.Write(entry);
            }
            catch (Exception loggingFailure)
            {
                // Logging must never take the request down.
                Trace.WriteLine("Logging failed: " + loggingFailure.Message + " / " + message);
            }
        }
    }
}
