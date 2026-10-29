using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Web.Models;

namespace Woodgrove.Statements.Web.Controllers
{
    public class CustomersController : Controller
    {
        public ActionResult Index(string q, string cc)
        {
            var customers = PortalServices.Customers;
            var model = new CustomerListModel
            {
                Query = q,
                CreditController = cc,
                CreditControllers = customers.CreditControllers(),
                Customers = customers.Search(q, cc)
            };
            if (model.Customers.Count == 1 && !string.IsNullOrWhiteSpace(q))
                return RedirectToAction("Details", new { id = model.Customers[0].CustomerNo });
            // Remember the last filter for the back link on the customer page.
            Session["CustomerFilter"] = Url.Action("Index", new { q, cc });
            return View(model);
        }

        public ActionResult Details(string id)
        {
            var customer = PortalServices.Customers.Get(id);
            if (customer == null) return HttpNotFound();
            var load = PortalServices.Ledger.LastLoad();
            var asOf = load?.LedgerDate ?? DateTime.Today;
            var lastMonthEnd = new DateTime(asOf.Year, asOf.Month, 1).AddDays(-1);
            var service = PortalServices.DocumentService;
            var model = new CustomerDetailsModel
            {
                Customer = customer,
                Current = service.BuildStatement(customer, new DateTime(asOf.Year, asOf.Month, 1), asOf),
                Contracts = PortalServices.Documents.ContractsForCustomer(customer.CustomerId),
                Deliveries = PortalServices.Deliveries.ForCustomer(customer.CustomerId, 20),
                DefaultPeriodStart = new DateTime(lastMonthEnd.Year, lastMonthEnd.Month, 1),
                DefaultStatementDate = lastMonthEnd,
                CanEditDelivery = User.IsCreditControl()
            };
            ViewBag.BackUrl = Session["CustomerFilter"] as string ?? Url.Action("Index");
            return View(model);
        }

        public ActionResult Statement(string id, DateTime periodStart, DateTime statementDate, string format = "pdf")
        {
            try
            {
                var doc = PortalServices.DocumentService.CustomerStatement(id, periodStart, statementDate, OutputFormats.Parse(format));
                LogRequest(ReportCodes.CustomerStatement, "CustomerNo=" + id + ";PeriodStart=" + periodStart.ToString("yyyy-MM-dd") + ";StatementDate=" + statementDate.ToString("yyyy-MM-dd"), format, doc.Content.Length);
                return File(doc.Content, doc.MimeType, doc.FileName);
            }
            catch (KeyNotFoundException)
            {
                return HttpNotFound();
            }
        }

        public ActionResult Invoice(string id)
        {
            try
            {
                var doc = PortalServices.DocumentService.InvoiceCopy(id);
                LogRequest(ReportCodes.InvoiceCopy, "InvoiceNo=" + id, "pdf", doc.Content.Length);
                return File(doc.Content, doc.MimeType, doc.FileName);
            }
            catch (KeyNotFoundException)
            {
                return HttpNotFound();
            }
        }

        [HttpPost, ValidateAntiForgeryToken, PortalRole(PortalRoles.CreditControl)]
        public ActionResult Delivery(DeliverySettingsModel model)
        {
            var customer = PortalServices.Customers.Get(model.CustomerNo);
            if (customer == null) return HttpNotFound();
            if (model.Delivery == StatementDelivery.Email && string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError("Email", "E-mail delivery needs an e-mail address.");
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelErrors());
                return RedirectToAction("Details", new { id = model.CustomerNo });
            }
            PortalServices.Customers.UpdateDelivery(customer.CustomerId, model.Delivery, model.Email, model.DropFolder);
            TempData["Message"] = "Statement delivery changed to " + model.Delivery + ".";
            return RedirectToAction("Details", new { id = model.CustomerNo });
        }

        IEnumerable<string> ModelErrors()
        {
            foreach (var v in ModelState.Values)
                foreach (var e in v.Errors)
                    yield return e.ErrorMessage;
        }

        void LogRequest(string code, string parameters, string format, int bytes)
        {
            PortalServices.Documents.LogRequest(new Core.Model.DocumentRequest
            {
                Channel = "PORTAL", RequestedBy = User.Identity.Name, ReportCode = code, Parameters = parameters,
                OutputFormat = format.ToUpperInvariant(), Bytes = bytes, Succeeded = true
            });
        }
    }
}
