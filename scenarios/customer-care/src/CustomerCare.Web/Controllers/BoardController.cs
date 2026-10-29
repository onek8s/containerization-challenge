using System;
using System.Linq;
using System.Web.Mvc;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Models;
using Tailspin.CustomerCare.Web.Realtime;

namespace Tailspin.CustomerCare.Web.Controllers
{
    /// <summary>Wall-screen queue board. Updates itself through the queue hub.</summary>
    public class BoardController : Controller
    {
        private static readonly CaseStatus[] Statuses = { CaseStatus.New, CaseStatus.Assigned, CaseStatus.InProgress, CaseStatus.WaitingOnCustomer };

        public ActionResult Index(string queue)
        {
            var queues = ReferenceData.ActiveQueues;
            var selected = string.IsNullOrEmpty(queue) ? null : ReferenceData.QueueByCode(queue);
            if (!string.IsNullOrEmpty(queue) && selected == null) return HttpNotFound();

            return View(new BoardModel { Queue = selected, Queues = queues, Columns = Load(selected) });
        }

        [OutputCache(Duration = 0, NoStore = true)]
        public ActionResult Data(string queue)
        {
            var selected = string.IsNullOrEmpty(queue) ? null : ReferenceData.QueueByCode(queue);
            return Json(Load(selected), JsonRequestBehavior.AllowGet);
        }

        private static System.Collections.Generic.IList<BoardColumn> Load(Queue queue)
        {
            var open = new CaseRepository().ListOpen()
                .Where(c => queue == null || c.QueueId == queue.Id)
                .OrderByDescending(c => c.Priority)
                .ThenBy(c => c.ResolveDueUtc)
                .ToList();

            return Statuses.Select(s => new BoardColumn
            {
                Status = s,
                Title = CaseWorkflow.DisplayName(s),
                Cases = open.Where(c => c.Status == s).Select(c => QueueNotifier.ToMessage(c, c.Queue, null)).ToList()
            }).ToList();
        }
    }
}
