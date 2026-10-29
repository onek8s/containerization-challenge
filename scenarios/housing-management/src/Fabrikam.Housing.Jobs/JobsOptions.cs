using System.Collections.Generic;

namespace Fabrikam.Housing.Jobs
{
    /// <summary>Bound from the "Jobs" configuration section.</summary>
    public class JobsOptions
    {
        /// <summary>Time zone the cron schedules are evaluated in.</summary>
        public string TimeZone { get; set; } = "GMT Standard Time";
        public int WorkerCount { get; set; } = 4;
        public bool PrepareSchemaIfNecessary { get; set; }
        public string PrintFolder { get; set; } = @"D:\HousingData\print";
        public string HousingBenefitInbound { get; set; } = @"D:\Interfaces\HB\Inbound";
        public string FinanceOutbound { get; set; } = @"D:\Interfaces\Finance\Outbound";
        public string BacsArchiveFolder { get; set; } = @"D:\Interfaces\Bacs\Sent";
        public string IncomeTeamEmail { get; set; } = "income.team@fabrikamhomes.co.uk";
        public string RepairsDeskEmail { get; set; } = "repairs.desk@fabrikamhomes.co.uk";
        public string ComplianceTeamEmail { get; set; } = "compliance@fabrikamhomes.co.uk";
        public int GeocodeBatchSize { get; set; } = 200;
        public Dictionary<string, string> Schedules { get; set; } = new Dictionary<string, string>();
    }
}
