using System;
using System.Linq;
using System.Web.Mvc;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Tracking.Models;

namespace VanArsdel.Integration.Tracking.Controllers
{
    /// <summary>Interchanges and their functional acknowledgements (997 / CONTRL).</summary>
    public class InterchangesController : TrackingController
    {
        private readonly InterchangeRepository _interchanges;

        public InterchangesController(InterchangeRepository interchanges)
        {
            _interchanges = interchanges;
        }

        public ActionResult Index(string partnerId, int days = 7, bool overdueOnly = false)
        {
            days = Math.Max(1, Math.Min(days, 90));
            var scope = Principal.Scope(partnerId);
            var now = DateTime.UtcNow;
            var list = overdueOnly
                ? _interchanges.PendingAcks().Where(i => i.IsAckOverdue(now))
                : _interchanges.Search(scope, now.AddDays(-days), now);

            var model = new InterchangeListModel
            {
                PartnerId = scope,
                Days = days,
                OverdueOnly = overdueOnly,
                NowUtc = now,
                Interchanges = list.Where(i => (scope == null || i.PartnerId == scope) && Principal.MaySee(i.PartnerId)).OrderByDescending(i => i.CreatedUtc).Take(1000).ToList()
            };

            return View(model);
        }
    }
}
