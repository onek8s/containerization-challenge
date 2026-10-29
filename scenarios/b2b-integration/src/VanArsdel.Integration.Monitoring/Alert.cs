using System;
using VanArsdel.Integration.Domain.Sla;

namespace VanArsdel.Integration.Monitoring
{
    public class Alert
    {
        /// <summary>Stable identity: same condition gives the same key in every cycle (dedup and clearing).</summary>
        public string Key { get; set; }

        public string SlaCode { get; set; }
        public SlaKind Kind { get; set; }
        public AlertSeverity Severity { get; set; }
        public string PartnerId { get; set; }
        public string Subject { get; set; }
        public string Detail { get; set; }
        public DateTime RaisedUtc { get; set; }
        public string NotifyGroup { get; set; }

        public override string ToString()
        {
            return "[" + Severity + "] " + SlaCode + " " + Subject;
        }
    }
}
