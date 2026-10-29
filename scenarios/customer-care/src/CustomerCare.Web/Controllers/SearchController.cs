using System.Linq;
using System.Web.Mvc;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Models;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web.Controllers
{
    public class SearchController : Controller
    {
        public ActionResult Index(string q)
        {
            q = (q ?? string.Empty).Trim();
            if (q.Length == 0) return View(new SearchModel { Query = q, Hits = new SearchHit[0], Articles = new KbArticle[0] });

            // A case number goes straight to the case.
            var number = CaseNumber.Normalize(q, LocalTime.Now.Year);
            if (number != null)
            {
                var c = new CaseRepository().GetByNumber(number);
                if (c != null) return Redirect(Url.Content("~/cases/" + c.Id));
            }

            return View(new SearchModel
            {
                Query = q,
                Hits = new ReportRepository().GlobalSearch(q),
                Articles = new KnowledgeRepository().Search(q, false, 5)
            });
        }

        /// <summary>Type-ahead for the search box.</summary>
        [OutputCache(Duration = 0, NoStore = true)]
        public ActionResult Suggest(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3) return Json(new object[0], JsonRequestBehavior.AllowGet);

            var hits = new ReportRepository().GlobalSearch(q.Trim()).Select(h => new
            {
                h.Kind,
                h.Title,
                h.Detail,
                Url = Url.Content(h.Kind == "case" ? "~/cases/" + h.Id : "~/customers/" + h.Id)
            });
            return Json(hits, JsonRequestBehavior.AllowGet);
        }
    }
}
