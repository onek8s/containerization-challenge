using System.Data.SqlTypes;
using System.Diagnostics;
using Microsoft.SqlServer.Server;

namespace ContosoFoods.TradeRebates.SqlClr.Integration
{
    public static class EventLogWriter
    {
        public const string LogName = "Contoso Trade Rebates";
        public const string Source = "TradeRebates.Database";

        /// <summary>rebate.clr_WriteEvent: writes to the application's event log on the database server.</summary>
        [SqlProcedure(Name = "clr_WriteEvent")]
        public static void WriteEvent(SqlInt32 eventId, [SqlFacet(MaxSize = 1)] SqlString level, [SqlFacet(MaxSize = 4000)] SqlString message)
        {
            EventLogEntryType type;
            switch (level.IsNull ? "I" : level.Value)
            {
                case "E": type = EventLogEntryType.Error; break;
                case "W": type = EventLogEntryType.Warning; break;
                default: type = EventLogEntryType.Information; break;
            }

            using (var log = new EventLog(LogName) { Source = Source })
                log.WriteEntry(message.IsNull ? string.Empty : message.Value, type, eventId.IsNull ? 1000 : eventId.Value);
        }
    }
}
