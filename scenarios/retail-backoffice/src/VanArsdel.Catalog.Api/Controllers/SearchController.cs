using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanArsdel.Catalog.Api.Search;

namespace VanArsdel.Catalog.Api.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchController : ControllerBase
    {
        private readonly IProductSearch _search;
        private readonly SearchIndexer _indexer;

        public SearchController(IProductSearch search, SearchIndexer indexer)
        {
            _search = search;
            _indexer = indexer;
        }

        /// <summary>Product search with facets. Open to the web shop through the gateway.</summary>
        [HttpGet]
        [AllowAnonymous]
        public Task<ProductSearchResult> Search([FromQuery] ProductSearchQuery query, CancellationToken cancellationToken)
        {
            query.IncludeDrafts = query.IncludeDrafts && User.Identity?.IsAuthenticated == true;
            query.PageSize = System.Math.Clamp(query.PageSize, 1, 100);
            return _search.SearchAsync(query, cancellationToken);
        }

        /// <summary>Rebuilds the search index from the database (weekly job and after index changes).</summary>
        [HttpPost("reindex")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> Reindex(CancellationToken cancellationToken)
        {
            var count = await _indexer.ReindexAllAsync(cancellationToken);
            return Ok(new { indexed = count });
        }
    }
}
