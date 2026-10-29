using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutoMapper;
using log4net;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Management.Wmi;
using VanArsdel.Integration.Monitoring;
using VanArsdel.Integration.OpsPortal.Models;

namespace VanArsdel.Integration.OpsPortal
{
    public partial class DefaultPage : OpsPage
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DefaultPage));
        private IList<SuspendedInstanceGroup> _suspended;
        private IList<HostRow> _hosts;
        private IList<Alert> _alerts;

        [Dependency]
        public BizTalkDatabaseQueries MessageBox { get; set; }

        [Dependency]
        public IHostInstances HostInstances { get; set; }

        [Dependency]
        public MonitoringRepository Monitoring { get; set; }

        public IEnumerable<SuspendedInstanceGroup> GetSuspendedGroups()
        {
            return _suspended ?? (_suspended = MessageBox.SuspendedGroups().OrderByDescending(g => g.Count).ToList());
        }

        public IEnumerable<HostRow> GetHosts()
        {
            if (_hosts == null)
            {
                try
                {
                    _hosts = Mapper.Map<List<HostRow>>(HostInstances.All()).OrderBy(h => h.HostName).ThenBy(h => h.ServerName).ToList();
                }
                catch (Exception ex)
                {
                    // WMI needs the portal account in BizTalk Operators and DCOM access to the servers
                    Log.Warn("Host instance query failed", ex);
                    _hosts = new List<HostRow>();
                }
            }

            return _hosts;
        }

        public IEnumerable<Alert> GetAlerts()
        {
            return _alerts ?? (_alerts = Monitoring.RecentAlerts(1).Take(25).ToList());
        }

        protected void Page_PreRender(object sender, EventArgs e)
        {
            var suspended = GetSuspendedGroups().Sum(g => g.Count);
            var down = GetHosts().Count(h => !h.Running && !h.Disabled && h.State != "NotApplicable");
            SuspendedCount.Text = suspended.ToString(CultureInfo.CurrentCulture);
            HostsDown.Text = down.ToString(CultureInfo.CurrentCulture);
            AlertCount.Text = GetAlerts().Count().ToString(CultureInfo.CurrentCulture);
            SpoolDepth.Text = MessageBox.SpoolDepth().ToString("N0", CultureInfo.CurrentCulture);
            SuspendedTile.Attributes["class"] = suspended > 0 ? "tile bad" : "tile";
            HostsTile.Attributes["class"] = down > 0 ? "tile bad" : "tile";
        }
    }
}
