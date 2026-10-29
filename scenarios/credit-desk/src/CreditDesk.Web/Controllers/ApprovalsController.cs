using System.Linq;
using System.ServiceModel;
using System.Web.Mvc;
using CreditDesk.Contracts;
using CreditDesk.Web.Models;
using CreditDesk.Web.Services;

namespace CreditDesk.Web.Controllers
{
    public class ApprovalsController : Controller
    {
        public ActionResult Index()
        {
            var pending = CreditServiceClient.Call(svc => svc.GetPendingApprovals())
                .OrderBy(r => r.RequestedAt).ToList();
            return View(pending);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Decide(DecisionModel decision)
        {
            try
            {
                var result = CreditServiceClient.Call(svc => svc.Decide(decision.RequestId, decision.Approve, decision.Comment));
                TempData["Notice"] = "Request " + result.RequestId + " " + (decision.Approve ? "approved" : "rejected")
                    + ". The customer letter is being prepared.";
            }
            catch (FaultException<CreditFault> fault)
            {
                TempData["Error"] = fault.Detail.Message;
            }
            return RedirectToAction("Index");
        }
    }
}
