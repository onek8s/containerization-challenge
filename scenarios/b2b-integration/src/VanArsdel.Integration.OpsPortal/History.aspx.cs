using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Monitoring;

namespace VanArsdel.Integration.OpsPortal
{
    public partial class HistoryPage : OpsPage
    {
        [Dependency]
        public ResubmitRepository Resubmissions { get; set; }

        [Dependency]
        public MonitoringRepository Monitoring { get; set; }

        private int Days
        {
            get { return int.Parse(DaysFilter.SelectedValue, CultureInfo.InvariantCulture); }
        }

        public IEnumerable<ResubmitEntry> GetResubmissions()
        {
            return Resubmissions.Recent(Days);
        }

        public IEnumerable<Alert> GetAlerts()
        {
            return Monitoring.RecentAlerts(Days);
        }

        protected void Days_Changed(object sender, EventArgs e)
        {
            ResubmitGrid.DataBind();
            AlertGrid.PageIndex = 0;
            AlertGrid.DataBind();
        }
    }
}
