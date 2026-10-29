using System;
using System.Web.Mvc;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;

namespace Tailspin.DealerOrders.SalesDesk.Controllers
{
    /// <summary>Commands waiting for, running in or failed in AX (ax.Command, processed by the AX connector service).</summary>
    [DeskAuthorize(DeskRole.SalesDesk, DeskRole.SalesManager)]
    public class AxQueueController : DeskController
    {
        private readonly DeskQueries _queries;
        private readonly SqlAxCommandQueue _queue;

        public AxQueueController(DeskQueries queries, SqlAxCommandQueue queue)
        {
            _queries = queries;
            _queue = queue;
        }

        public ActionResult Index(AxCommandState? state)
        {
            ViewBag.State = state;
            ViewBag.Counts = _queue.Counts();
            return View(_queries.AxCommands(state));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Requeue(long id)
        {
            _queue.Requeue(id, DeskUser, DateTime.UtcNow);
            Flash($"Command {id} queued again.");
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken, DeskAuthorize(DeskRole.SalesManager)]
        public ActionResult Cancel(long id)
        {
            _queue.Cancel(id, DeskUser);
            Flash($"Command {id} cancelled.");
            return RedirectToAction("Index");
        }
    }
}
