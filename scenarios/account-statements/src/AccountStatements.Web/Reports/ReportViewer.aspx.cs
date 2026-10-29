using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Web.UI;
using Microsoft.Reporting.WebForms;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Reporting;

namespace Woodgrove.Statements.Web.Reports
{
    /// <summary>
    /// Interactive reports (aged debt with drillthrough to the customer statement, portfolio summary). Local processing:
    /// the viewer keeps the processed report in session state between postbacks and export requests.
    /// </summary>
    public partial class ReportViewerPage : Page
    {
        string ReportCode
        {
            get { return (Request.QueryString["report"] ?? ReportCodes.AgedDebt).ToUpperInvariant(); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RoleMap.IsIn(User, PortalRoles.User))
            {
                Response.StatusCode = 403;
                Response.End();
                return;
            }
            if (IsPostBack) return;

            var info = ReportDefinitionInfo.Find(ReportCode);
            if (info == null || info.ResourceName == null || info.PerCustomer)
            {
                ErrorText.Text = "Unknown report.";
                Viewer.Visible = false;
                return;
            }
            ReportTitle.Text = info.Title;
            ControllerPanel.Visible = info.Parameters.Contains("CreditController");
            CreditController.DataSource = PortalServices.Customers.CreditControllers();
            CreditController.DataBind();
            CreditController.SelectedValue = Request.QueryString["controller"] ?? "";

            DateTime asOf;
            if (!DateTime.TryParseExact(Request.QueryString["asOf"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out asOf))
                asOf = PortalServices.Ledger.LastLoad()?.LedgerDate ?? DateTime.Today.AddDays(-1);
            AsOf.Text = asOf.ToString("yyyy-MM-dd");
            Bind();
        }

        protected void Refresh_Click(object sender, EventArgs e)
        {
            Bind();
        }

        void Bind()
        {
            DateTime asOf;
            if (!DateTime.TryParseExact(AsOf.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out asOf))
            {
                ErrorText.Text = "Enter a valid date.";
                return;
            }
            var service = PortalServices.DocumentService;
            ReportJob job;
            switch (ReportCode)
            {
                case ReportCodes.AgedDebt:
                    var controller = string.IsNullOrEmpty(CreditController.SelectedValue) ? null : CreditController.SelectedValue;
                    job = service.AgedDebtJob(service.AgedDebtRows(asOf, controller), asOf, controller);
                    break;
                case ReportCodes.Portfolio:
                    job = service.PortfolioJob(service.PortfolioRows(asOf), asOf);
                    break;
                default:
                    ErrorText.Text = "This report cannot be viewed here.";
                    return;
            }
            Viewer.Reset();
            Viewer.ProcessingMode = ProcessingMode.Local;
            Apply(Viewer.LocalReport, job);
            Viewer.LocalReport.Refresh();
        }

        /// <summary>Aged debt customer number opens that customer's statement inside the viewer.</summary>
        protected void Viewer_Drillthrough(object sender, DrillthroughEventArgs e)
        {
            var report = (LocalReport)e.Report;
            var parameters = report.OriginalParametersToDrillthrough.ToDictionary(p => p.Name, p => p.Values.Cast<string>().FirstOrDefault());
            string customerNo;
            if (e.ReportPath != "CustomerStatement" || !parameters.TryGetValue("CustomerNo", out customerNo))
            {
                e.Cancel = true;
                return;
            }
            var statementDate = DateTime.Parse(parameters["StatementDate"], CultureInfo.InvariantCulture);
            var periodStart = DateTime.Parse(parameters["PeriodStart"], CultureInfo.InvariantCulture);
            var service = PortalServices.DocumentService;
            var customer = PortalServices.Customers.Get(customerNo);
            if (customer == null)
            {
                e.Cancel = true;
                return;
            }
            Apply(report, service.StatementJob(service.BuildStatement(customer, periodStart, statementDate)));
        }

        static void Apply(LocalReport report, ReportJob job)
        {
            report.EnableExternalImages = false;
            report.DisplayName = ReportDefinitions.ReportName(job.Definition);
            using (var stream = ReportDefinitions.Open(job.Definition))
                report.LoadReportDefinition(stream);
            foreach (var sub in job.Subreports)
                using (var stream = ReportDefinitions.Open(sub.Value))
                    report.LoadSubreportDefinition(sub.Key, stream);
            if (job.SubreportData != null)
            {
                var data = job.SubreportData;
                report.SubreportProcessing += (s, e) =>
                {
                    foreach (var name in e.DataSourceNames)
                        e.DataSources.Add(new ReportDataSource(name, data(e.ReportPath, name) ?? new object[0]));
                };
            }
            report.DataSources.Clear();
            foreach (var ds in job.DataSources)
                report.DataSources.Add(new ReportDataSource(ds.Key, (IEnumerable)ds.Value));
            report.SetParameters(job.Parameters.Select(p => new ReportParameter(p.Key, p.Value)).ToArray());
        }
    }
}
