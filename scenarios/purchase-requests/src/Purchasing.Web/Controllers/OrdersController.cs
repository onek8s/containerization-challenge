using System.Net;
using System.Web.Mvc;
using Contoso.Purchasing.Web.Models;
using Contoso.Purchasing.Web.Services;

namespace Contoso.Purchasing.Web.Controllers
{
    public class OrdersController : PortalController
    {
        public ActionResult Index()
        {
            if (!(CurrentUser.IsBuyer || CurrentUser.IsFinance)) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            ViewBag.ReadyToOrder = CurrentUser.IsBuyer ? Api.Requests("ordering") : null;
            return View(Api.Orders());
        }

        public ActionResult Create(int requestId)
        {
            if (!CurrentUser.IsBuyer) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            var form = new OrderForm
            {
                RequestId = requestId,
                DeliveryAddress = Session["Purchasing.LastDeliveryAddress"] as string
                    ?? "Contoso Manufacturing, Goods Receiving\r\nTehtaankatu 12\r\n33100 Tampere",
                Request = Api.Request(requestId)
            };
            return View(form);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(OrderForm form)
        {
            if (!ModelState.IsValid)
            {
                form.Request = Api.Request(form.RequestId);
                return View(form);
            }

            try
            {
                var order = Api.CreateOrder(form.RequestId, form.DeliveryAddress);
                Session["Purchasing.LastDeliveryAddress"] = form.DeliveryAddress;
                Flash("Purchase order " + order.PoNumber + " created.");
                return RedirectToAction("Print", new { id = order.Id });
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError("", ex.Message);
                form.Request = Api.Request(form.RequestId);
                return View(form);
            }
        }

        public ActionResult Print(int id)
        {
            return View(Api.Order(id));
        }
    }
}
