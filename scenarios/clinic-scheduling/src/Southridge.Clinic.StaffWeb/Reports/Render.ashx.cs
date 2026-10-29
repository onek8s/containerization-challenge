using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.SessionState;
using Southridge.Clinic.Data;
using Southridge.Clinic.Documents;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.StaffWeb.Reports
{
    /// <summary>
    /// Renders a report as PDF or Excel: Render.ashx?report=ArAging&amp;AsOf=2014-03-31&amp;format=Excel. Report server first,
    /// RDLC fallback (Clinic:ReportMode). format=Xls gives the NPOI workbook instead of the report layout.
    /// </summary>
    public class RenderHandler : IHttpHandler, IReadOnlySessionState
    {
        private static readonly string[] BillingReports = { ReportNames.ArAging, ReportNames.ClaimSummary };

        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (!context.User.Identity.IsAuthenticated)
            {
                context.Response.StatusCode = 401;
                return;
            }

            string name = context.Request.QueryString["report"];
            if (!ReportNames.All.Contains(name))
            {
                context.Response.StatusCode = 404;
                return;
            }

            if (BillingReports.Contains(name) && !(context.User.IsInRole(ClinicRoles.Billing) || context.User.IsInRole(ClinicRoles.Manager) || context.User.IsInRole(ClinicRoles.Administrator)))
            {
                context.Response.StatusCode = 403;
                return;
            }

            var request = new ReportRequest(name);
            foreach (string key in context.Request.QueryString.AllKeys)
            {
                if (key == null || key == "report" || key == "format")
                {
                    continue;
                }

                string value = context.Request.QueryString[key];
                DateTime date;
                if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    request.With(key, date);
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    request.With(key, value);
                }
            }

            string format = context.Request.QueryString["format"] ?? "Pdf";
            byte[] content;
            string contentType, extension;
            if (string.Equals(format, "Xls", StringComparison.OrdinalIgnoreCase))
            {
                DataTable table = LocalReportRenderer.Fill(request);
                var aging = table as ReportsDataSet.ArAgingDataTable;
                content = aging != null
                    ? ExcelExports.AgingWorkbook(aging, (DateTime)request.Parameters["AsOf"])
                    : ExcelExports.TableWorkbook(table, name);
                contentType = "application/vnd.ms-excel";
                extension = "xls";
            }
            else
            {
                request.Format = string.Equals(format, "Excel", StringComparison.OrdinalIgnoreCase) ? ReportFormat.Excel : ReportFormat.Pdf;
                RenderedReport report = new ReportRendererFactory().Create().Render(request);
                content = report.Content;
                contentType = report.MimeType;
                extension = report.Extension;
                context.Response.AddHeader("X-Rendered-By", report.RenderedBy);
            }

            ClinicLog.Audit(context.User.Identity.Name, "Report", name + " " + format);
            context.Response.ContentType = contentType;
            context.Response.AddHeader("Content-Disposition", "inline; filename=" + name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + "." + extension);
            context.Response.BinaryWrite(content);
        }
    }
}
