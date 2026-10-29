using System;
using System.Linq;
using System.Web.Mvc;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Core.Scheduling;
using Woodgrove.Statements.Web.Models;

namespace Woodgrove.Statements.Web.Controllers
{
    public class ReportsController : Controller
    {
        public ActionResult Index()
        {
            var load = PortalServices.Ledger.LastLoad();
            return View(new ReportCatalogModel
            {
                Reports = ReportDefinitionInfo.All.Where(r => !r.PerCustomer).ToList(),
                CreditControllers = PortalServices.Customers.CreditControllers(),
                DefaultAsOf = load?.LedgerDate ?? DateTime.Today.AddDays(-1)
            });
        }

        /// <summary>Downloads a report directly (Excel workbooks and PDF), without the viewer page.</summary>
        public ActionResult Download(string code, string format, DateTime? asOf, DateTime? from, DateTime? to, string creditController)
        {
            var info = ReportDefinitionInfo.Find(code);
            if (info == null || info.PerCustomer) return HttpNotFound();
            var parameters = new ParameterSet();
            if (asOf.HasValue) parameters["AsOf"] = asOf.Value.ToString(ParameterSet.DateFormat);
            if (from.HasValue) parameters["From"] = from.Value.ToString(ParameterSet.DateFormat);
            if (to.HasValue) parameters["To"] = to.Value.ToString(ParameterSet.DateFormat);
            if (!string.IsNullOrWhiteSpace(creditController)) parameters["CreditController"] = creditController;
            var outputFormat = OutputFormats.Parse(format ?? "pdf");
            var started = DateTime.Now;
            var doc = PortalServices.DocumentService.Render(info.Code, parameters, outputFormat, DateTime.Now);
            PortalServices.Documents.LogRequest(new DocumentRequest
            {
                Channel = "PORTAL", RequestedBy = User.Identity.Name, ReportCode = info.Code, Parameters = parameters.ToString(),
                OutputFormat = outputFormat.ToString().ToUpperInvariant(), Bytes = doc.Content.Length,
                DurationMs = (int)(DateTime.Now - started).TotalMilliseconds, Succeeded = true
            });
            return File(doc.Content, doc.MimeType, doc.FileName);
        }
    }
}
