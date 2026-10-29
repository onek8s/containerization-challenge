using System;
using System.Web.UI.WebControls;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web.Cases
{
    public partial class CaseList : CarePage
    {
        private const string FilterKey = "Cases.Filter";

        /// <summary>The filter is kept in the session so that agents come back to the same list from a case.</summary>
        private CaseSearchFilter Filter
        {
            get
            {
                var f = Session[FilterKey] as CaseSearchFilter;
                if (f == null)
                {
                    f = new CaseSearchFilter { OpenOnly = true, SortColumn = "ResolveDueUtc" };
                    Session[FilterKey] = f;
                }

                return f;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            FilterQueue.DataSource = ReferenceData.ActiveQueues;
            FilterQueue.DataBind();

            FilterStatus.Items.Add(new ListItem("Open", "open"));
            FilterStatus.Items.Add(new ListItem("All", string.Empty));
            foreach (CaseStatus s in Enum.GetValues(typeof(CaseStatus)))
            {
                FilterStatus.Items.Add(new ListItem(CaseWorkflow.DisplayName(s), ((int)s).ToString()));
            }

            var q = Request.QueryString["queue"];
            if (!string.IsNullOrEmpty(q))
            {
                var queue = ReferenceData.QueueByCode(q);
                if (queue != null) Filter.QueueId = queue.Id;
            }

            ShowFilter();
            Bind();
        }

        protected void ApplyButton_Click(object sender, EventArgs e)
        {
            var f = Filter;
            f.Text = FilterText.Text;
            f.QueueId = string.IsNullOrEmpty(FilterQueue.SelectedValue) ? (int?)null : int.Parse(FilterQueue.SelectedValue);
            f.OpenOnly = FilterStatus.SelectedValue == "open";
            f.Status = FilterStatus.SelectedValue == "open" || FilterStatus.SelectedValue == string.Empty ? (CaseStatus?)null : (CaseStatus)int.Parse(FilterStatus.SelectedValue);
            f.AssignedTo = FilterMine.Checked ? AccountName : null;
            f.BreachedOnly = FilterBreached.Checked;
            f.PageIndex = 0;
            Bind();
        }

        protected void ResetButton_Click(object sender, EventArgs e)
        {
            Session.Remove(FilterKey);
            ShowFilter();
            Bind();
        }

        protected void CaseGrid_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            Filter.PageIndex = e.NewPageIndex;
            Bind();
        }

        protected void CaseGrid_Sorting(object sender, GridViewSortEventArgs e)
        {
            var f = Filter;
            f.SortDescending = f.SortColumn == e.SortExpression && !f.SortDescending;
            f.SortColumn = e.SortExpression;
            f.PageIndex = 0;
            Bind();
        }

        protected void CaseGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;
            var item = (CaseListItem)e.Row.DataItem;
            if (item.EscalationLevel >= 2) e.Row.CssClass = "breached";
            else if (item.EscalationLevel == 1) e.Row.CssClass = "at-risk";
            if (item.Priority == CasePriority.Critical) e.Row.CssClass += " critical";
        }

        private void ShowFilter()
        {
            var f = Filter;
            FilterText.Text = f.Text;
            FilterQueue.SelectedValue = f.QueueId.HasValue ? f.QueueId.Value.ToString() : string.Empty;
            FilterStatus.SelectedValue = f.OpenOnly ? "open" : f.Status.HasValue ? ((int)f.Status.Value).ToString() : string.Empty;
            FilterMine.Checked = !string.IsNullOrEmpty(f.AssignedTo);
            FilterBreached.Checked = f.BreachedOnly;
        }

        private void Bind()
        {
            var f = Filter;
            f.PageSize = CaseGrid.PageSize;
            var result = new CaseRepository().Search(f);

            CaseGrid.VirtualItemCount = result.TotalRows;
            CaseGrid.PageIndex = f.PageIndex;
            CaseGrid.DataSource = result.Items;
            CaseGrid.DataBind();

            ResultCount.Text = result.TotalRows == 1 ? "1 case" : result.TotalRows + " cases";
        }
    }
}
