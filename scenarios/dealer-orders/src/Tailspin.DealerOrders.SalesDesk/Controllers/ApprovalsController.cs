using System.Web.Mvc;
using Tailspin.DealerOrders.Processing.Sales;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;

namespace Tailspin.DealerOrders.SalesDesk.Controllers
{
    [DeskAuthorize(DeskRole.SalesDesk, DeskRole.SalesManager)]
    public class ApprovalsController : DeskController
    {
        private readonly DeskQueries _queries;
        private readonly ApprovalService _approvals;

        public ApprovalsController(DeskQueries queries, ApprovalService approvals)
        {
            _queries = queries;
            _approvals = approvals;
        }

        public ActionResult Index() => View(_queries.PendingApprovals());

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Decide(int id, bool approve, decimal? discount, string comment)
        {
            _approvals.Decide(id, DeskUser, IsManager, approve, discount, comment, AxUser);
            Flash(approve ? "Approved." : "Rejected; the dealer is told the reason.");
            return RedirectToAction("Index");
        }
    }
}
