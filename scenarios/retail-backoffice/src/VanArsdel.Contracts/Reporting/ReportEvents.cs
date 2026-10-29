using System;
using System.Collections.Generic;

namespace VanArsdel.Contracts.Reporting
{
    public class ReportReady
    {
        public string ReportName { get; set; }
        public DateTime BusinessDate { get; set; }
        public string FileName { get; set; }
        public string DownloadUrl { get; set; }
        public List<string> Recipients { get; set; } = new List<string>();
    }
}
