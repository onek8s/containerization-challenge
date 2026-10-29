using System;

namespace Fabrikam.Housing.Common
{
    public class AuditEntry
    {
        public long Id { get; set; }
        public DateTime At { get; set; }
        public string Actor { get; set; }
        public string Application { get; set; }
        public string EntityType { get; set; }
        public string EntityKey { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
    }

    public class BankHoliday
    {
        public DateTime Date { get; set; }
        public string Name { get; set; }
    }
}
