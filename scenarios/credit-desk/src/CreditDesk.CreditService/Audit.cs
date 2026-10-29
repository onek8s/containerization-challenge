using System;
using System.Diagnostics;

namespace CreditDesk.CreditService
{
    internal static class Audit
    {
        public const string Source = "Credit Desk Service";

        public static void Write(string message, EventLogEntryType type, int eventId)
        {
            try
            {
                EventLog.WriteEntry(Source, message, type, eventId);
            }
            catch (Exception)
            {
                // Source missing (not installed with install-credit-service.ps1) or no access.
                Trace.TraceInformation("{0}: {1}", eventId, message);
            }
        }
    }
}
