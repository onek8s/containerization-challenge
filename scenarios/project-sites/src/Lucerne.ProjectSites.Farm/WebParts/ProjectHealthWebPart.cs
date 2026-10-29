using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls.WebParts;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core.Mapping;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;

namespace Lucerne.ProjectSites.Farm.WebParts
{
    /// <summary>Hub home page: RAG table of all live projects, optionally filtered by office.</summary>
    [ToolboxItem(false)]
    public class ProjectHealthWebPart : WebPart
    {
        [WebBrowsable(true), Personalizable(PersonalizationScope.Shared), WebDisplayName("Office"), Category("Lucerne")]
        public string OfficeFilter { get; set; }

        [WebBrowsable(true), Personalizable(PersonalizationScope.Shared), WebDisplayName("Show only amber and red"), Category("Lucerne")]
        public bool ExceptionsOnly { get; set; }

        protected override void RenderContents(HtmlTextWriter writer)
        {
            var web = SPContext.Current.Web;
            var list = web.Lists.TryGetList(ListNames.ProjectStatus);
            if (list == null)
            {
                writer.Write("The Project Status list is missing. Activate the Lucerne hub feature.");
                return;
            }

            var conditions = new[]
            {
                string.IsNullOrEmpty(OfficeFilter) ? null : Caml.Eq(FieldNames.Office, "Text", OfficeFilter),
                ExceptionsOnly ? Caml.In(FieldNames.OverallStatus, "Choice", "Amber", "Red") : null
            };
            var query = new SPQuery
            {
                Query = Caml.Where(Caml.And(conditions)) + Caml.OrderBy(FieldNames.ProjectNumber),
                RowLimit = 500
            };

            var html = new StringBuilder("<table class=\"luc-health\"><tr><th>Project</th><th>Office</th><th>Overall</th><th>Schedule</th><th>Budget</th><th>Risk</th><th>Summary</th></tr>");
            foreach (SPListItem item in list.GetItems(query))
            {
                var data = new ListItemData(item.ID, item.Fields.Cast<SPField>().Where(f => !f.Hidden).ToDictionary(f => f.InternalName, f => item[f.Id]));
                var s = ListMapper.ToStatus(data);
                html.Append("<tr><td>").Append(SPHttpUtility.HtmlEncode(item.Title)).Append("</td><td>")
                    .Append(SPHttpUtility.HtmlEncode(data.GetString(FieldNames.Office))).Append("</td>")
                    .Append(Cell(s.Overall)).Append(Cell(s.Schedule)).Append(Cell(s.Budget)).Append(Cell(s.Risk))
                    .Append("<td>").Append(SPHttpUtility.HtmlEncode(s.Summary)).Append("</td></tr>");
            }

            html.Append("</table>");
            writer.Write(html.ToString());
        }

        private static string Cell(RagStatus status)
        {
            return "<td class=\"luc-rag luc-rag-" + status.ToString().ToLowerInvariant() + "\">" + (status == RagStatus.NotAssessed ? "-" : status.ToString()) + "</td>";
        }
    }

    /// <summary>Hub home page: projects where the current user is project manager or director.</summary>
    [ToolboxItem(false)]
    public class MyProjectsWebPart : WebPart
    {
        protected override void RenderContents(HtmlTextWriter writer)
        {
            var web = SPContext.Current.Web;
            var list = web.Lists.TryGetList(ListNames.Projects);
            if (list == null)
            {
                return;
            }

            var query = new SPQuery
            {
                Query = Caml.Where(Caml.And(
                    Caml.Or(Caml.EqCurrentUser(FieldNames.ProjectManager), Caml.EqCurrentUser(FieldNames.ProjectDirector)),
                    Caml.Neq(FieldNames.ProjectStage, "Choice", "Closed"))) + Caml.OrderBy(FieldNames.ProjectNumber),
                RowLimit = 100
            };

            writer.Write("<ul class=\"luc-myprojects\">");
            foreach (SPListItem item in list.GetItems(query))
            {
                var url = item[FieldNames.SiteUrl] as string;
                writer.Write("<li>");
                if (!string.IsNullOrEmpty(url))
                {
                    writer.Write("<a href=\"" + SPHttpUtility.HtmlUrlAttributeEncode(url) + "\">" + SPHttpUtility.HtmlEncode(item.Title) + "</a>");
                }
                else
                {
                    writer.Write(SPHttpUtility.HtmlEncode(item.Title) + " <em>(site pending)</em>");
                }

                writer.Write(" <span class=\"luc-stage\">" + SPHttpUtility.HtmlEncode(item[FieldNames.ProjectStage] as string) + "</span></li>");
            }

            writer.Write("</ul>");
        }
    }

    /// <summary>Project site home page: deliverables due in the next N weeks and overdue ones.</summary>
    [ToolboxItem(false)]
    public class UpcomingDeliverablesWebPart : WebPart
    {
        public UpcomingDeliverablesWebPart()
        {
            Weeks = 4;
        }

        [WebBrowsable(true), Personalizable(PersonalizationScope.Shared), WebDisplayName("Weeks ahead"), Category("Lucerne")]
        public int Weeks { get; set; }

        protected override void RenderContents(HtmlTextWriter writer)
        {
            var list = SPContext.Current.Site.RootWeb.Lists.TryGetList(ListNames.Deliverables);
            if (list == null)
            {
                return;
            }

            var horizon = DateTime.Today.AddDays(7 * Weeks);
            var query = new SPQuery
            {
                Query = Caml.Where(Caml.And(
                    Caml.Leq(FieldNames.ForecastDate, "DateTime", horizon),
                    Caml.In(FieldNames.DeliverableStatus, "Choice", "Planned", "WorkInProgress", "ForReview", "Approved")))
                    + Caml.OrderBy(FieldNames.ForecastDate),
                RowLimit = 50
            };

            writer.Write("<table class=\"luc-deliverables\"><tr><th>Number</th><th>Title</th><th>Rev</th><th>Status</th><th>Forecast</th></tr>");
            foreach (SPListItem item in list.GetItems(query))
            {
                var forecast = item[FieldNames.ForecastDate] as DateTime?;
                var overdue = forecast.HasValue && forecast.Value < DateTime.Today;
                writer.Write("<tr" + (overdue ? " class=\"luc-overdue\"" : string.Empty) + "><td>"
                    + SPHttpUtility.HtmlEncode(item[FieldNames.DocumentNumber] as string) + "</td><td>"
                    + SPHttpUtility.HtmlEncode(item.Title) + "</td><td>"
                    + SPHttpUtility.HtmlEncode(item[FieldNames.Revision] as string) + "</td><td>"
                    + SPHttpUtility.HtmlEncode(item[FieldNames.DeliverableStatus] as string) + "</td><td>"
                    + (forecast.HasValue ? forecast.Value.ToString("yyyy-MM-dd") : string.Empty) + "</td></tr>");
            }

            writer.Write("</table>");
        }
    }
}
