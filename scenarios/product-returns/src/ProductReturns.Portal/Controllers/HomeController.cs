using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ProductReturns.Portal.Models;
using ProductReturns.Portal.Services;

namespace ProductReturns.Portal.Controllers
{
    public class HomeController : Controller
    {
        public const string DraftKey = "ReturnDraft";

        private readonly ReturnsApiClient _api;

        public HomeController(ReturnsApiClient api)
        {
            _api = api;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new StartReturnModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(StartReturnModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var orderNumber = NormalizeOrderNumber(model.OrderNumber);
            var order = await _api.GetOrderAsync(orderNumber, model.Email.Trim(), ct);
            if (!order.Ok)
            {
                ModelState.AddModelError(string.Empty, order.StatusCode == 404
                    ? "We could not find an order with this number and e-mail address."
                    : order.Error);
                return View(model);
            }

            HttpContext.Session.SetObject(DraftKey, new ReturnDraft
            {
                OrderNumber = order.Value.OrderNumber,
                Email = model.Email.Trim(),
                CustomerName = order.Value.CustomerName
            });
            return RedirectToAction("Select", "Return");
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public static string NormalizeOrderNumber(string value)
        {
            var digits = value.Trim().ToUpperInvariant().Replace("SO-", string.Empty).Replace("SO", string.Empty);
            return "SO-" + digits;
        }
    }
}
