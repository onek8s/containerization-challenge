using System;
using System.Linq;
using System.Web.UI.WebControls;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Farm.Common;
using Lucerne.ProjectSites.ServerObjectModel;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.WebControls;

namespace Lucerne.ProjectSites.Farm.Pages
{
    /// <summary>_layouts/15/Lucerne.ProjectSites/RetentionPreview.aspx: what the retention job will do with this project.</summary>
    public class RetentionPreviewPage : LayoutsPageBase
    {
        protected SPGridView DecisionsGrid;
        protected Literal Summary;
        protected Button ApplyButton;

        protected override bool RequireSiteAdministrator
        {
            get { return true; }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyButton.Click += Apply;
            ApplyButton.OnClientClick = "return confirm('Apply retention to this project now? Deleted files go to the site collection recycle bin.');";
            if (IsPostBack)
            {
                return;
            }

            var result = Service().Run(DateTime.Today, true, FarmSettings.ProjectNumber(SPContext.Current.Site.RootWeb));
            Summary.Text = string.Format(
                "{0} documents: {1} to declare as records, {2} to archive, {3} to delete, {4} kept.",
                result.Decisions.Count, result.Count(RetentionAction.DeclareRecord), result.Count(RetentionAction.MoveToArchive),
                result.Count(RetentionAction.Delete), result.Count(RetentionAction.Keep));

            DecisionsGrid.AutoGenerateColumns = false;
            DecisionsGrid.Columns.Add(new BoundField { DataField = "ItemUrl", HeaderText = "Document" });
            DecisionsGrid.Columns.Add(new BoundField { DataField = "Category", HeaderText = "Category" });
            DecisionsGrid.Columns.Add(new BoundField { DataField = "Action", HeaderText = "Action" });
            DecisionsGrid.Columns.Add(new BoundField { DataField = "DueDate", HeaderText = "Due", DataFormatString = "{0:yyyy-MM-dd}" });
            DecisionsGrid.Columns.Add(new BoundField { DataField = "Reason", HeaderText = "Reason" });
            DecisionsGrid.DataSource = result.Decisions.OrderByDescending(d => d.Action).ToList();
            DecisionsGrid.DataBind();
            ApplyButton.Enabled = result.Decisions.Any(d => d.Action != RetentionAction.Keep);
        }

        private void Apply(object sender, EventArgs e)
        {
            using (var operation = new SPLongOperation(this))
            {
                operation.LeadingHTML = "Applying retention";
                operation.Begin();
                Service().Run(DateTime.Today, false, FarmSettings.ProjectNumber(SPContext.Current.Site.RootWeb));
                operation.End(Request.RawUrl, SPRedirectFlags.Static, Context, null);
            }
        }

        private RetentionService Service()
        {
            return new RetentionService(new SsomSiteConnector(true), FarmSettings.HubUrl(SPContext.Current.Web), new RetentionPolicy(), new UlsActivityLog("RetentionPreview"));
        }
    }
}
