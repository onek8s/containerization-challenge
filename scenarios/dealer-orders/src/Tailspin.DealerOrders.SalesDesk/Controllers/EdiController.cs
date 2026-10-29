using System.Text;
using System.Web.Mvc;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Edi;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;

namespace Tailspin.DealerOrders.SalesDesk.Controllers
{
    [DeskAuthorize(DeskRole.SalesDesk, DeskRole.SalesManager)]
    public class EdiController : DeskController
    {
        private readonly DeskQueries _queries;
        private readonly IEdiStore _store;
        private readonly EdiOrderIntake _intake;

        public EdiController(DeskQueries queries, IEdiStore store, EdiOrderIntake intake)
        {
            _queries = queries;
            _store = store;
            _intake = intake;
        }

        public ActionResult Index(EdiMessageState? state, int? partnerId)
        {
            ViewBag.State = state;
            ViewBag.Partners = _store.Partners();
            return View(_queries.EdiMessages(state, partnerId));
        }

        public ActionResult Message(int id)
        {
            var message = _store.GetMessage(id);
            if (message == null) return HttpNotFound();
            ViewBag.Partner = _store.GetPartner(message.PartnerId);
            return View(message);
        }

        public ActionResult Raw(int id)
        {
            var message = _store.GetMessage(id);
            if (message == null) return HttpNotFound();
            return File(Encoding.ASCII.GetBytes(message.Content), "text/plain", $"{message.TransactionSet}_{message.InterchangeControl}.x12");
        }

        /// <summary>Runs a received interchange through order intake again (after the dealer or item data was fixed).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Reprocess(int id)
        {
            var message = _store.GetMessage(id);
            if (message == null || message.Direction != EdiDirection.Inbound) return HttpNotFound();
            var result = _intake.Receive(message.Content, message.As2MessageId, _store.GetPartner(message.PartnerId));
            Flash($"Processed again: {result.CreatedOrders.Count} orders, {result.Rejected.Count} rejected.");
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Resend(int id)
        {
            var message = _store.GetMessage(id);
            if (message == null || message.Direction != EdiDirection.Outbound) return HttpNotFound();
            message.State = EdiMessageState.Queued;
            message.Attempts = 0;
            message.Error = null;
            _store.SaveMessage(message);
            Flash($"{message.TransactionSet} {message.Reference} queued to be sent again.");
            return RedirectToAction("Message", new { id });
        }
    }
}
