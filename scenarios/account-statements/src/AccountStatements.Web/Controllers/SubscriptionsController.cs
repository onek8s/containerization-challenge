using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Woodgrove.Statements.Core.Delivery;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Core.Scheduling;
using Woodgrove.Statements.Web.Models;

namespace Woodgrove.Statements.Web.Controllers
{
    public class SubscriptionsController : Controller
    {
        public ActionResult Index()
        {
            var all = PortalServices.Subscriptions.List();
            if (!User.IsAdmin()) all = all.Where(s => IsOwner(s)).ToList();
            ViewBag.Runs = PortalServices.Subscriptions.Runs(null, 25);
            return View(all);
        }

        public ActionResult Create()
        {
            return View("Edit", Prepare(new SubscriptionEditModel
            {
                ReportCode = ReportCodes.AgedDebt, Schedule = "WEEKLY MON 07:00", Parameters = "AsOf={Yesterday}", OutputFormat = "EXCEL",
                DeliveryMethod = DeliveryMethods.Email, Recipients = User.Identity.Name.Split('\\').Last() + "@woodgrove-leasing.fi",
                FileNameTemplate = "Report_{RunDate:yyyyMMdd}", Owner = User.Identity.Name
            }));
        }

        public ActionResult Edit(int id)
        {
            var s = PortalServices.Subscriptions.Get(id);
            if (s == null) return HttpNotFound();
            if (!IsOwner(s) && !User.IsAdmin()) return new HttpStatusCodeResult(403);
            return View(Prepare(new SubscriptionEditModel
            {
                SubscriptionId = s.SubscriptionId, Name = s.Name, ReportCode = s.ReportCode, Schedule = s.Schedule, Parameters = s.Parameters,
                OutputFormat = s.OutputFormat, DeliveryMethod = s.DeliveryMethod, Recipients = s.Recipients, SharePath = s.SharePath,
                FileNameTemplate = s.FileNameTemplate, EmailSubject = s.EmailSubject, Enabled = s.Enabled, Owner = s.Owner
            }));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(SubscriptionEditModel model)
        {
            Subscription existing = null;
            if (model.SubscriptionId != 0)
            {
                existing = PortalServices.Subscriptions.Get(model.SubscriptionId);
                if (existing == null) return HttpNotFound();
                if (!IsOwner(existing) && !User.IsAdmin()) return new HttpStatusCodeResult(403);
            }
            var report = ReportDefinitionInfo.Find(model.ReportCode);
            if (report == null) ModelState.AddModelError("ReportCode", "Choose a report.");
            Schedule schedule = null;
            string error;
            if (!string.IsNullOrWhiteSpace(model.Schedule) && !Schedule.TryParse(model.Schedule, out schedule, out error)) ModelState.AddModelError("Schedule", error);
            try
            {
                var p = ParameterSet.Parse(model.Parameters);
                p.Resolve(DateTime.Today);
                if (report != null)
                    foreach (var name in p.Items.Select(i => i.Key).Where(n => !report.Parameters.Contains(n, StringComparer.OrdinalIgnoreCase)))
                        ModelState.AddModelError("Parameters", report.Title + " has no parameter " + name + ".");
            }
            catch (FormatException ex)
            {
                ModelState.AddModelError("Parameters", ex.Message);
            }
            try
            {
                var format = OutputFormats.Parse(model.OutputFormat);
                if (report != null && !report.Formats.Contains(format)) ModelState.AddModelError("OutputFormat", report.Title + " is not available as " + model.OutputFormat + ".");
            }
            catch (FormatException ex)
            {
                ModelState.AddModelError("OutputFormat", ex.Message);
            }
            if (model.ReportCode == ReportCodes.StatementRun && model.DeliveryMethod != DeliveryMethods.Customer)
                ModelState.AddModelError("DeliveryMethod", "Statement runs deliver by each customer's own setting.");
            if (model.ReportCode != ReportCodes.StatementRun && model.DeliveryMethod == DeliveryMethods.Customer)
                ModelState.AddModelError("DeliveryMethod", "Customer delivery is only for statement runs.");
            if (model.DeliveryMethod == DeliveryMethods.Email)
            {
                var recipients = RecipientList.Parse(model.Recipients);
                if (recipients.Count == 0) ModelState.AddModelError("Recipients", "Enter at least one recipient.");
                foreach (var bad in RecipientList.Invalid(recipients)) ModelState.AddModelError("Recipients", "Invalid address " + bad + ".");
            }
            if (model.DeliveryMethod == DeliveryMethods.Share && (string.IsNullOrWhiteSpace(model.SharePath) || !model.SharePath.StartsWith(@"\\")))
                ModelState.AddModelError("SharePath", @"Enter a UNC path, e.g. \\files01.corp.local\Finance$\AR.");
            if (model.ReportCode == ReportCodes.StatementRun && !User.IsCreditControl())
                ModelState.AddModelError("ReportCode", "Only credit control can schedule statement runs.");

            if (!ModelState.IsValid) return View("Edit", Prepare(model));

            var s = existing ?? new Subscription { Owner = User.Identity.Name };
            s.Name = model.Name.Trim();
            s.ReportCode = model.ReportCode;
            s.Schedule = schedule.ToString();
            s.Parameters = string.IsNullOrWhiteSpace(model.Parameters) ? null : ParameterSet.Parse(model.Parameters).ToString();
            s.OutputFormat = model.OutputFormat.ToUpperInvariant();
            s.DeliveryMethod = model.DeliveryMethod;
            s.Recipients = model.Recipients;
            s.SharePath = model.SharePath;
            s.FileNameTemplate = model.FileNameTemplate;
            s.EmailSubject = model.EmailSubject;
            s.Enabled = model.Enabled;
            s.NextRunAt = model.Enabled ? schedule.NextRun(DateTime.Now, PortalServices.Documents.Holidays()) : (DateTime?)null;
            s.ModifiedBy = User.Identity.Name;
            var id = PortalServices.Subscriptions.Save(s);
            TempData["Message"] = "Subscription saved. Next run " + (s.NextRunAt.HasValue ? s.NextRunAt.Value.ToString("dd.MM.yyyy HH:mm") : "not scheduled") + ".";
            return RedirectToAction("Edit", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RunNow(int id)
        {
            var s = PortalServices.Subscriptions.Get(id);
            if (s == null) return HttpNotFound();
            if (!IsOwner(s) && !User.IsAdmin()) return new HttpStatusCodeResult(403);
            PortalServices.Subscriptions.RunNow(id, User.Identity.Name);
            TempData["Message"] = "The distribution service runs '" + s.Name + "' within a minute.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Disable(int id)
        {
            var s = PortalServices.Subscriptions.Get(id);
            if (s == null) return HttpNotFound();
            if (!IsOwner(s) && !User.IsAdmin()) return new HttpStatusCodeResult(403);
            PortalServices.Subscriptions.Delete(id);
            TempData["Message"] = "'" + s.Name + "' disabled.";
            return RedirectToAction("Index");
        }

        public ActionResult Run(int id)
        {
            var run = PortalServices.Subscriptions.Runs(null, 500).FirstOrDefault(r => r.RunId == id);
            if (run == null) return HttpNotFound();
            return View(new RunDetailsModel { Run = run, Deliveries = PortalServices.Deliveries.ForRun(id) });
        }

        [HttpPost, ValidateAntiForgeryToken, PortalRole(PortalRoles.CreditControl)]
        public ActionResult Resend(long id, int runId)
        {
            PortalServices.Deliveries.Resend(id);
            TempData["Message"] = "Delivery queued for resending.";
            return RedirectToAction("Run", new { id = runId });
        }

        public ActionResult Preview(string schedule)
        {
            Schedule s;
            string error;
            if (!Schedule.TryParse(schedule, out s, out error)) return Json(new { error }, JsonRequestBehavior.AllowGet);
            var holidays = PortalServices.Documents.Holidays();
            var runs = new List<string>();
            var next = DateTime.Now;
            for (var i = 0; i < 5; i++)
            {
                next = s.NextRun(next, holidays);
                runs.Add(next.ToString("ddd dd.MM.yyyy HH:mm"));
            }
            return Json(new { description = s.Describe(), runs }, JsonRequestBehavior.AllowGet);
        }

        SubscriptionEditModel Prepare(SubscriptionEditModel model)
        {
            model.Reports = ReportDefinitionInfo.All.Where(r => r.Code != ReportCodes.CustomerStatement && r.Code != ReportCodes.InvoiceCopy).ToList();
            Schedule schedule;
            string error;
            if (Schedule.TryParse(model.Schedule, out schedule, out error))
            {
                var holidays = PortalServices.Documents.Holidays();
                var next = DateTime.Now;
                for (var i = 0; i < 3; i++) model.NextRuns.Add(next = schedule.NextRun(next, holidays));
            }
            return model;
        }

        bool IsOwner(Subscription s)
        {
            return string.Equals(s.Owner, User.Identity.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
