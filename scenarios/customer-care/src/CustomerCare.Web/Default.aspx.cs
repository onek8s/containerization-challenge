using System;
using System.Collections.Generic;
using System.Web.Caching;
using System.Web.UI.WebControls;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Realtime;
using Tailspin.CustomerCare.Web.Security;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web
{
    public partial class _Default : CarePage
    {
        private class DashboardData
        {
            public IList<QueueStats> Queues;
            public IList<AgentWorkload> Workload;
            public DateTime LoadedUtc;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var data = LoadDashboard();
            UpdatedAt.Text = LocalTime.Short(data.LoadedUtc);

            QueueTiles.DataSource = data.Queues;
            QueueTiles.DataBind();

            MyCases.DataSource = new CaseRepository().Search(new CaseSearchFilter
            {
                AssignedTo = AccountName,
                OpenOnly = true,
                SortColumn = "ResolveDueUtc",
                PageSize = 50
            }).Items;
            MyCases.DataBind();

            if (CurrentUser.IsSupervisor)
            {
                WorkloadSection.Visible = true;
                Workload.DataSource = data.Workload;
                Workload.DataBind();
            }
        }

        protected void MyCases_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;
            var item = (CaseListItem)e.Row.DataItem;
            if (item.EscalationLevel >= 2) e.Row.CssClass = "breached";
            else if (item.EscalationLevel == 1) e.Row.CssClass = "at-risk";
        }

        /// <summary>The figures are shared by everybody and kept for a minute; case changes clear them.</summary>
        private DashboardData LoadDashboard()
        {
            var data = Cache[QueueNotifier.DashboardKey] as DashboardData;
            if (data != null) return data;

            var reports = new ReportRepository();
            data = new DashboardData
            {
                Queues = reports.QueueStats(LocalTime.TodayStartUtc),
                Workload = reports.AgentWorkload(),
                LoadedUtc = DateTime.UtcNow
            };
            Cache.Insert(QueueNotifier.DashboardKey, data, null, DateTime.UtcNow.AddMinutes(1), Cache.NoSlidingExpiration);
            return data;
        }
    }
}
