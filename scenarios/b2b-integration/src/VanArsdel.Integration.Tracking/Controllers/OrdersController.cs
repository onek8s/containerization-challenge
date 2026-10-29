using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Monitoring;
using VanArsdel.Integration.Tracking.Exports;
using VanArsdel.Integration.Tracking.Models;

namespace VanArsdel.Integration.Tracking.Controllers
{
    public class OrdersController : TrackingController
    {
        private readonly TrackingRepository _tracking;
        private readonly InterchangeRepository _interchanges;
        private readonly PartnerRepository _partners;

        public OrdersController(TrackingRepository tracking, InterchangeRepository interchanges, PartnerRepository partners)
        {
            _tracking = tracking;
            _interchanges = interchanges;
            _partners = partners;
        }

        public ActionResult Index(OrderSearchModel model)
        {
            model = model ?? new OrderSearchModel();
            model.Partners = Principal.IsInternal
                ? _partners.LoadPartners().Where(p => p.IsCustomer).Select(p => p.PartnerId).OrderBy(p => p).ToList()
                : new List<string> { Principal.PartnerId };

            if (Request.QueryString.Count == 0)
            {
                // first visit: what is still open
                model.Results = Scope(_tracking.OpenOrders()).OrderByDescending(o => o.ReceivedUtc).Take(200).ToList();
                return View(model);
            }

            model.Searched = true;
            model.Results = Search(model);
            return View(model);
        }

        public ActionResult Export(OrderSearchModel model)
        {
            var rows = Search(model ?? new OrderSearchModel());
            var title = string.Format(CultureInfo.InvariantCulture, "Orders {0:yyyy-MM-dd} - {1:yyyy-MM-dd}{2}",
                model == null || !model.From.HasValue ? DateTime.Today.AddDays(-14) : model.From.Value,
                model == null || !model.To.HasValue ? DateTime.Today : model.To.Value,
                Principal.Scope(model == null ? null : model.PartnerId) == null ? string.Empty : " " + Principal.Scope(model.PartnerId));
            return Excel(WorkbookExporter.Orders(rows, title), "orders");
        }

        public ActionResult Details(string partnerId, string orderNumber)
        {
            if (!Principal.MaySee(partnerId))
            {
                return HttpNotFound();
            }

            var order = _tracking.FindOrder(partnerId, orderNumber);
            if (order == null)
            {
                return HttpNotFound();
            }

            var model = new OrderDetailsModel { Order = order, Steps = Steps(order) };
            if (order.ReceivedUtc.HasValue)
            {
                // outbound documents for the same partner around the order's lifetime (ack, ASN, invoice)
                var to = (order.InvoicedUtc ?? DateTime.UtcNow).AddDays(1);
                model.Interchanges = _interchanges.Search(partnerId, order.ReceivedUtc.Value.AddHours(-1), to).Take(50).ToList();
            }

            return View(model);
        }

        internal static IList<MilestoneStep> Steps(OrderActivityRow o)
        {
            var steps = new List<MilestoneStep>
            {
                Step("Received", o.ReceivedUtc, o),
                Step("Validated", o.ValidatedUtc, o)
            };

            if (o.CreditHoldUtc.HasValue)
            {
                var hold = Step("Credit hold", o.CreditHoldUtc, o);
                hold.Problem = !o.ReleasedUtc.HasValue;
                steps.Add(hold);
                steps.Add(Step("Released", o.ReleasedUtc, o));
            }

            if (o.RejectedUtc.HasValue)
            {
                var rejected = Step("Rejected", o.RejectedUtc, o);
                rejected.Problem = true;
                steps.Add(rejected);
                return steps;
            }

            steps.Add(Step("Sent to ERP", o.SentToErpUtc, o));
            steps.Add(Step("Confirmed (855/ORDRSP)", o.ConfirmedUtc, o));
            steps.Add(Step("Despatched", o.DespatchedUtc, o));
            steps.Add(Step("ASN sent", o.AsnSentUtc, o));
            steps.Add(Step("Invoiced", o.InvoicedUtc, o));
            return steps;
        }

        private static MilestoneStep Step(string name, DateTime? at, OrderActivityRow o)
        {
            return new MilestoneStep
            {
                Name = name,
                AtUtc = at,
                SinceReceived = at.HasValue && o.ReceivedUtc.HasValue ? SlaEvaluator.Format(at.Value - o.ReceivedUtc.Value) : string.Empty
            };
        }

        private IList<OrderActivityRow> Search(OrderSearchModel model)
        {
            var search = new OrderSearch
            {
                PartnerId = Principal.Scope(model.PartnerId),
                OrderNumber = Trim(model.OrderNumber),
                ErpOrderNumber = Trim(model.ErpOrderNumber),
                FromUtc = StartOfDayUtc(model.From, 14),
                ToUtc = EndOfDayUtc(model.To),
                Status = string.IsNullOrEmpty(model.Status) ? null : model.Status,
                MaxRows = 2000
            };

            return Scope(_tracking.SearchOrders(search)).ToList();
        }

        private IEnumerable<OrderActivityRow> Scope(IEnumerable<OrderActivityRow> rows)
        {
            var principal = Principal;
            return rows.Where(r => principal.MaySee(r.PartnerId));
        }

        private static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
