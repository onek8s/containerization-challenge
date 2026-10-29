using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanArsdel.Catalog.Api.Data;

namespace VanArsdel.Catalog.Api.Search
{
    public class SearchIndexer
    {
        private readonly CatalogDbContext _db;
        private readonly IProductSearch _search;
        private readonly ILogger<SearchIndexer> _logger;

        public SearchIndexer(CatalogDbContext db, IProductSearch search, ILogger<SearchIndexer> logger)
        {
            _db = db;
            _search = search;
            _logger = logger;
        }

        public async Task<int> ReindexAllAsync(CancellationToken cancellationToken)
        {
            var products = await _db.Products.AsNoTracking()
                .Include(p => p.Brand).Include(p => p.Category).ThenInclude(c => c.Parent).Include(p => p.Skus)
                .ToListAsync(cancellationToken);
            var docs = products.Select(ProductDocument.From).ToList();
            await _search.IndexManyAsync(docs, cancellationToken);
            _logger.LogInformation("Reindexed {Count} products", docs.Count);
            return docs.Count;
        }

        public async Task IndexAsync(Guid productId, CancellationToken cancellationToken)
        {
            var product = await _db.Products.AsNoTracking()
                .Include(p => p.Brand).Include(p => p.Category).ThenInclude(c => c.Parent).Include(p => p.Skus)
                .SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);
            if (product != null) await _search.IndexAsync(ProductDocument.From(product), cancellationToken);
        }
    }

    /// <summary>Fills the in-memory index at start-up.</summary>
    public class SearchIndexWarmup : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SearchIndexWarmup> _logger;

        public SearchIndexWarmup(IServiceScopeFactory scopeFactory, ILogger<SearchIndexWarmup> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<SearchIndexer>().ReindexAllAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search warm-up failed");
            }
        }
    }
}
