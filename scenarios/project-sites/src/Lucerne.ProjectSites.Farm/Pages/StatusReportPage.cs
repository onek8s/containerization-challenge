using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Documents;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Farm.Common;
using Lucerne.ProjectSites.ServerObjectModel;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.WebControls;

namespace Lucerne.ProjectSites.Farm.Pages
{
    /// <summary>_layouts/15/Lucerne.ProjectSites/StatusReport.aspx: current status and the monthly Word report.</summary>
    public class StatusReportPage : LayoutsPageBase
    {
        protected Literal ProjectTitle;
        protected Literal OverallStatus;
        protected Literal StatusDetails;
        protected SPGridView DeliverablesGrid;
        protected Button GenerateButton;
        protected Label Message;

        protected override SPBasePermissions RightsRequired
        {
            get { return SPBasePermissions.EditListItems; }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GenerateButton.Click += Generate;
            if (IsPostBack)
            {
                return;
            }

            var snapshot = ReadSnapshot();
            var status = new StatusAssessor().Assess(snapshot, DateTime.Today);
            ProjectTitle.Text = SPHttpUtility.HtmlEncode(snapshot.Project.Number + " " + snapshot.Project.Name);
            OverallStatus.Text = "<span class=\"luc-rag luc-rag-" + status.Overall.ToString().ToLowerInvariant() + "\">" + status.Overall + "</span>";
            StatusDetails.Text = SPHttpUtility.HtmlEncode(status.Summary);

            DeliverablesGrid.AutoGenerateColumns = false;
            AddColumn("DocumentNumber", "Number");
            AddColumn("Title", "Title");
            AddColumn("Revision", "Rev");
            AddColumn("Status", "Status");
            AddColumn("ForecastDate", "Forecast", "{0:yyyy-MM-dd}");
            DeliverablesGrid.DataSource = snapshot.Deliverables
                .Where(d => d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Superseded)
                .OrderBy(d => d.ForecastDate ?? d.PlannedDate ?? DateTime.MaxValue)
                .ToList();
            DeliverablesGrid.DataBind();
        }

        private void Generate(object sender, EventArgs e)
        {
            var user = SPContext.Current.Web.CurrentUser.Name;
            using (var operation = new SPLongOperation(this))
            {
                operation.LeadingHTML = "Creating the status report";
                operation.TrailingHTML = "Reading the project lists and building the document. This can take a minute on large projects.";
                operation.Begin();

                var snapshot = ReadSnapshot();
                var status = new StatusAssessor().Assess(snapshot, DateTime.Today);
                var bytes = StatusReportDocument.Create(snapshot, status, user, DateTime.Today);

                var web = SPContext.Current.Site.RootWeb;
                var library = web.Lists[Discipline.Find("PMG").LibraryName];
                var folder = EnsureFolder(library, "Reports");
                var name = snapshot.Project.Number + "-status-" + DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".docx";
                var file = folder.Files.Add(name, bytes, true);
                file.Item[FieldNames.DocumentType] = "RPT";
                file.Item.SystemUpdate(false);

                operation.End(web.Url + "/" + file.Url, SPRedirectFlags.Static, Context, null);
            }
        }

        private ProjectSnapshot ReadSnapshot()
        {
            var web = SPContext.Current.Site.RootWeb;
            var number = FarmSettings.ProjectNumber(web);
            var connector = new SsomSiteConnector(true);
            Project project;
            using (var hub = connector.Open(FarmSettings.HubUrl(web)))
            {
                var entry = new HubRepository(hub).FindProject(number);
                if (entry == null)
                {
                    throw new SPException("Project " + number + " is not in the project register.");
                }

                project = entry.Value.Value;
            }

            return new StatusRollupService(connector, FarmSettings.HubUrl(web), null, new UlsActivityLog("StatusReport")).ReadSnapshot(project);
        }

        private static SPFolder EnsureFolder(SPList library, string name)
        {
            var url = library.RootFolder.ServerRelativeUrl.TrimEnd('/') + "/" + name;
            var folder = library.ParentWeb.GetFolder(url);
            return folder.Exists ? folder : library.RootFolder.SubFolders.Add(name);
        }

        private void AddColumn(string field, string header, string format = null)
        {
            DeliverablesGrid.Columns.Add(new BoundField { DataField = field, HeaderText = header, DataFormatString = format });
        }
    }
}
