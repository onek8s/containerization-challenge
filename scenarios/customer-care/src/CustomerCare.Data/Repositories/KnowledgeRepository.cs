using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Data.Repositories
{
    public class KnowledgeRepository
    {
        public IList<KbArticle> List(bool includeDrafts)
        {
            using (var db = new CareDbContext())
            {
                IQueryable<KbArticle> q = db.KbArticles.AsNoTracking();
                if (!includeDrafts) q = q.Where(a => a.IsPublished);
                return q.OrderBy(a => a.Title).ToList();
            }
        }

        public IList<KbArticle> Search(string text, bool includeDrafts, int max)
        {
            return List(includeDrafts)
                .Select(a => new { Article = a, Score = KnowledgeText.Score(a, text) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Article.Title)
                .Take(max)
                .Select(x => x.Article)
                .ToList();
        }

        public IList<KbArticle> ForCategory(string categoryCode, int max)
        {
            using (var db = new CareDbContext())
            {
                return db.KbArticles.AsNoTracking().Where(a => a.IsPublished && a.CategoryCode == categoryCode)
                    .OrderByDescending(a => a.Views).Take(max).ToList();
            }
        }

        public KbArticle GetBySlug(string slug)
        {
            using (var db = new CareDbContext())
            {
                return db.KbArticles.AsNoTracking().FirstOrDefault(a => a.Slug == slug);
            }
        }

        public KbArticle Get(int id)
        {
            using (var db = new CareDbContext())
            {
                return db.KbArticles.AsNoTracking().FirstOrDefault(a => a.Id == id);
            }
        }

        public void IncrementViews(int id)
        {
            using (var db = new CareDbContext())
            {
                db.Database.ExecuteSqlCommand("UPDATE care.KbArticle SET Views = Views + 1 WHERE Id = @p0", id);
            }
        }

        public KbArticle Save(KbArticle article, string user)
        {
            using (var db = new CareDbContext())
            {
                article.Body = KnowledgeText.Sanitize(article.Body);
                article.Slug = KnowledgeText.Slugify(article.Title);
                article.ModifiedBy = user;
                article.ModifiedUtc = DateTime.UtcNow;

                if (db.KbArticles.Any(a => a.Slug == article.Slug && a.Id != article.Id))
                {
                    article.Slug = (article.Slug.Length > 70 ? article.Slug.Substring(0, 70) : article.Slug) + "-" + DateTime.UtcNow.ToString("yyMMddHHmm");
                }

                if (article.Id == 0)
                {
                    db.KbArticles.Add(article);
                }
                else
                {
                    var existing = db.KbArticles.Find(article.Id);
                    if (existing == null) throw new InvalidOperationException("Article " + article.Id + " not found.");
                    existing.Title = article.Title;
                    existing.Slug = article.Slug;
                    existing.Body = article.Body;
                    existing.Tags = article.Tags;
                    existing.CategoryCode = article.CategoryCode;
                    existing.IsPublished = article.IsPublished;
                    existing.ModifiedBy = article.ModifiedBy;
                    existing.ModifiedUtc = article.ModifiedUtc;
                    article = existing;
                }

                db.SaveChanges();
                return article;
            }
        }
    }
}
