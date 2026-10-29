using System.Linq;
using System.Web.Mvc;
using CreditDesk.Web.Models;
using CreditDesk.Web.Services;

namespace CreditDesk.Web.Controllers
{
    public class CustomersController : Controller
    {
        public ActionResult Index(string q)
        {
            var model = new CustomerSearchModel { Query = q };
            if (!string.IsNullOrWhiteSpace(q))
                model.Results = CreditServiceClient.Call(svc => svc.SearchCustomers(q.Trim())).ToList();
            return View(model);
        }

        public ActionResult Details(string id)
        {
            return View(Load(id, new NewRequestModel { AccountNumber = id }));
        }

        internal static CustomerDetailsModel Load(string accountNumber, NewRequestModel form)
        {
            return CreditServiceClient.Call(svc => new CustomerDetailsModel
            {
                Customer = svc.GetCustomer(accountNumber),
                History = svc.GetRequestsForCustomer(accountNumber).OrderByDescending(r => r.RequestedAt).ToList(),
                NewRequest = form
            });
        }
    }
}
