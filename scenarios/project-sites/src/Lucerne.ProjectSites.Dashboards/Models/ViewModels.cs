using System.Collections.Generic;
using Lucerne.ProjectSites.Data.Entities;
using Lucerne.ProjectSites.Documents;
using Lucerne.ProjectSites.Domain;

namespace Lucerne.ProjectSites.Dashboards.Models
{
    public class PortfolioViewModel
    {
        public string Office { get; set; }
        public bool ExceptionsOnly { get; set; }
        public List<PortfolioRow> Rows { get; set; }
        public List<string> Offices { get; set; }
        public Dictionary<RagStatus, int> Counts { get; set; }

        public int Count(RagStatus status)
        {
            int n;
            return Counts != null && Counts.TryGetValue(status, out n) ? n : 0;
        }
    }

    public class ProjectViewModel
    {
        public ProjectViewModel()
        {
            Deliverables = new List<Deliverable>();
            Risks = new List<Risk>();
            History = new KpiSnapshot[0];
        }

        public Project Project { get; set; }
        public ProjectStatus Status { get; set; }
        public List<Deliverable> Deliverables { get; set; }
        public List<Risk> Risks { get; set; }
        public KpiSnapshot[] History { get; set; }
        public bool CanSeeSite { get; set; }
        public bool Subscribed { get; set; }
    }
}
