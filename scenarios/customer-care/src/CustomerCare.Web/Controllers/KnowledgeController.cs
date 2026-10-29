using System.Linq;
using System.Web.Mvc;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Models;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web.Controllers
{
    public class KnowledgeController : Controller
    {
        private readonly KnowledgeRepository _articles = new KnowledgeRepository();

        private bool CanEdit
        {
            get { return CareUser.IsIn(HttpContext, CareRole.Supervisor); }
        }

        public ActionResult Index(string q)
        {
            var model = new KnowledgeIndexModel
            {
                Query = q,
                CanEdit = CanEdit,
                Articles = string.IsNullOrWhiteSpace(q) ? _articles.List(CanEdit) : _articles.Search(q, CanEdit, 50)
            };
            return View(model);
        }

        public ActionResult Article(string slug)
        {
            var a = _articles.GetBySlug(slug);
            if (a == null || (!a.IsPublished && !CanEdit)) return HttpNotFound();

            _articles.IncrementViews(a.Id);
            return View(new ArticleModel
            {
                Article = a,
                CategoryName = a.CategoryCode != null ? ReferenceData.CategoryName(a.CategoryCode) : null,
                Tags = KnowledgeText.SplitTags(a.Tags),
                CanEdit = CanEdit
            });
        }

        [CareAuthorize(CareRole.Supervisor)]
        public ActionResult Edit(int? id)
        {
            var model = new ArticleEditModel();
            if (id.HasValue)
            {
                var a = _articles.Get(id.Value);
                if (a == null) return HttpNotFound();
                model.Id = a.Id;
                model.Title = a.Title;
                model.Body = a.Body;
                model.Tags = a.Tags;
                model.CategoryCode = a.CategoryCode;
                model.IsPublished = a.IsPublished;
            }

            model.Categories = Categories(model.CategoryCode);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CareAuthorize(CareRole.Supervisor)]
        public ActionResult Edit(ArticleEditModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Categories = Categories(model.CategoryCode);
                return View(model);
            }

            var saved = _articles.Save(new KbArticle
            {
                Id = model.Id,
                Title = model.Title.Trim(),
                Body = model.Body,
                Tags = model.Tags,
                CategoryCode = string.IsNullOrEmpty(model.CategoryCode) ? null : model.CategoryCode,
                IsPublished = model.IsPublished
            }, User.Identity.Name);

            TempData["Message"] = "Article saved.";
            return RedirectToRoute("kb-article", new { slug = saved.Slug });
        }

        private static SelectList Categories(string selected)
        {
            var items = ReferenceData.Categories.Select(c => new { c.Code, c.Name }).ToList();
            items.Insert(0, new { Code = string.Empty, Name = "(none)" });
            return new SelectList(items, "Code", "Name", selected ?? string.Empty);
        }
    }
}
