using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Realtime;

namespace Tailspin.CustomerCare.Web.Models
{
    public class KnowledgeIndexModel
    {
        public string Query { get; set; }
        public IList<KbArticle> Articles { get; set; }
        public bool CanEdit { get; set; }
    }

    public class ArticleModel
    {
        public KbArticle Article { get; set; }
        public string CategoryName { get; set; }
        public IList<string> Tags { get; set; }
        public bool CanEdit { get; set; }
    }

    public class ArticleEditModel
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required, AllowHtml]
        public string Body { get; set; }

        [StringLength(400)]
        public string Tags { get; set; }

        public string CategoryCode { get; set; }

        public bool IsPublished { get; set; }

        public IEnumerable<SelectListItem> Categories { get; set; }
    }

    public class BoardModel
    {
        public Queue Queue { get; set; }
        public IList<Queue> Queues { get; set; }
        public IList<BoardColumn> Columns { get; set; }
    }

    public class BoardColumn
    {
        public CaseStatus Status { get; set; }
        public string Title { get; set; }
        public IList<CaseChangeMessage> Cases { get; set; }
    }

    public class SearchModel
    {
        public string Query { get; set; }
        public IList<SearchHit> Hits { get; set; }
        public IList<KbArticle> Articles { get; set; }
    }
}
