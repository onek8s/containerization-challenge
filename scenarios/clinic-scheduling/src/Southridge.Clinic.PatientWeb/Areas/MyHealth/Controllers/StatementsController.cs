using System;
using System.Web.Mvc;
using Southridge.Clinic.Billing;
using Southridge.Clinic.PatientWeb.Areas.MyHealth.Models;

namespace Southridge.Clinic.PatientWeb.Areas.MyHealth.Controllers
{
    public class StatementsController : PatientControllerBase
    {
        private readonly IStatementSource _statements;

        public StatementsController(IStatementSource statements, IPatientContext patientContext)
            : base(patientContext)
        {
            _statements = statements;
        }

        public ActionResult Index()
        {
            PatientContext.Refresh();
            return View(new StatementsViewModel
            {
                Balance = Patient.Balance,
                Statements = _statements.Recent(Patient.PatientId),
                Claims = _statements.Claims(Patient.PatientId, DateTime.Today.AddMonths(-18))
            });
        }

        /// <summary>Current statement as a PDF, built on request (same layout as the mailed statement).</summary>
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public ActionResult Current()
        {
            StatementResult result = _statements.Current(Patient.PatientId);
            if (result == null)
            {
                TempData["Message"] = "There is nothing on your account to show.";
                return RedirectToAction("Index");
            }

            return File(result.Pdf, "application/pdf", "Southridge-statement-" + DateTime.Today.ToString("yyyy-MM-dd") + ".pdf");
        }
    }
}
