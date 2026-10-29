using System.ServiceModel;
using System.Web.Mvc;
using CreditDesk.Contracts;
using CreditDesk.Web.Models;
using CreditDesk.Web.Services;

namespace CreditDesk.Web.Controllers
{
    public class RequestsController : Controller
    {
        public ActionResult Details(int id)
        {
            return View(CreditServiceClient.Call(svc => svc.GetRequest(id)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Prefix = "NewRequest")] NewRequestModel form)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var request = CreditServiceClient.Call(svc => svc.SubmitRequest(form.AccountNumber, form.RequestedLimit.Value, form.Reason));
                    TempData["Notice"] = "Request " + request.RequestId + " was submitted (" + request.Status + ").";
                    return RedirectToAction("Details", new { id = request.RequestId });
                }
                catch (FaultException<CreditFault> fault)
                {
                    ModelState.AddModelError("", fault.Detail.Message);
                    foreach (var error in fault.Detail.Errors ?? new string[0])
                        ModelState.AddModelError("", error);
                }
            }
            return View("~/Views/Customers/Details.cshtml", CustomersController.Load(form.AccountNumber, form));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Withdraw(int id)
        {
            CreditServiceClient.Call(svc => svc.Withdraw(id));
            TempData["Notice"] = "Request " + id + " was withdrawn.";
            return RedirectToAction("Details", new { id });
        }
    }
}
