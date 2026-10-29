using System;
using System.Web.Mvc;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.Infrastructure.DocumentService;

namespace Tailspin.DealerOrders.DealerPortal.Controllers
{
    public class DocumentsController : PortalController
    {
        private readonly SqlDocumentStore _store;
        private readonly DocumentServiceClient _documents;

        public DocumentsController(SqlDocumentStore store, DocumentServiceClient documents)
        {
            _store = store;
            _documents = documents;
        }

        public ActionResult Index() => View(_store.List(DealerId, null, null));

        public ActionResult Download(int id)
        {
            var doc = _documents.Call(s => s.GetDocument(id));
            if (doc.DealerId != DealerId) return HttpNotFound();
            return File(doc.Content, doc.ContentType, doc.FileName);
        }

        public ActionResult PriceList()
        {
            var info = _documents.Call(s => s.GeneratePriceList(DealerId, UserName));
            return RedirectToAction("Download", new { id = info.Id });
        }

        [PortalRole(PortalRoles.DealerAdmin)]
        public ActionResult Agreement(int? year)
        {
            var info = _documents.Call(s => s.GenerateDealerContract(DealerId, year ?? DateTime.Today.Year, UserName));
            return RedirectToAction("Download", new { id = info.Id });
        }
    }
}
