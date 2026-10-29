using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.CustomerCare.Web.Realtime
{
    /// <summary>Who has which case open right now, so that two agents do not work on the same case.</summary>
    public static class CasePresence
    {
        private class Entry
        {
            public int CaseId;
            public string Name;
        }

        private static readonly ConcurrentDictionary<string, Entry> Connections = new ConcurrentDictionary<string, Entry>();

        public static IList<string> Enter(int caseId, string connectionId, string name)
        {
            Connections[connectionId] = new Entry { CaseId = caseId, Name = name };
            return Viewers(caseId);
        }

        public static bool Leave(string connectionId, out int caseId)
        {
            Entry e;
            if (Connections.TryRemove(connectionId, out e))
            {
                caseId = e.CaseId;
                return true;
            }

            caseId = 0;
            return false;
        }

        public static IList<string> Viewers(int caseId)
        {
            return Connections.Values.Where(e => e.CaseId == caseId).Select(e => e.Name).Distinct().OrderBy(n => n).ToList();
        }
    }
}
