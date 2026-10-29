using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VanArsdel.Catalog.Api.Search
{
    /// <summary>
    /// Used when no Elasticsearch cluster is configured (developer machines, test environments).
    /// The index is rebuilt from the database at start-up by <see cref="SearchIndexWarmup"/>.
    /// </summary>
    public class InMemoryProductSearch : IProductSearch
    {
        private readonly ConcurrentDictionary<string, ProductDocument> _documents = new ConcurrentDictionary<string, ProductDocument>();

        public Task IndexAsync(ProductDocument document, CancellationToken cancellationToken)
        {
            _documents[document.Id] = document;
            return Task.CompletedTask;
        }

        public Task IndexManyAsync(IReadOnlyCollection<ProductDocument> documents, CancellationToken cancellationToken)
        {
            foreach (var d in documents) _documents[d.Id] = d;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string id, CancellationToken cancellationToken)
        {
            _documents.TryRemove(id, out _);
            return Task.CompletedTask;
        }

        public Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken)
        {
            IEnumerable<ProductDocument> docs = _documents.Values;
            if (!query.IncludeDrafts) docs = docs.Where(d => d.Status == "Published");
            if (!string.IsNullOrEmpty(query.Category)) docs = docs.Where(d => Eq(d.Category, query.Category));
            if (!string.IsNullOrEmpty(query.Brand)) docs = docs.Where(d => Eq(d.Brand, query.Brand));
            if (!string.IsNullOrEmpty(query.Season)) docs = docs.Where(d => Eq(d.Season, query.Season));
            if (!string.IsNullOrEmpty(query.Colour)) docs = docs.Where(d => d.Colours.Any(c => Eq(c, query.Colour)));
            if (!string.IsNullOrEmpty(query.Size)) docs = docs.Where(d => d.Sizes.Any(s => Eq(s, query.Size)));

            var scored = docs.Select(d => new { d, score = Score(d, query.Text) }).Where(x => x.score > 0).ToList();
            var filtered = scored.Select(x => x.d).ToList();

            var result = new ProductSearchResult
            {
                Total = filtered.Count,
                Engine = "in-memory",
                Items = scored.OrderByDescending(x => x.score).ThenBy(x => x.d.StyleCode)
                    .Skip((Math.Max(query.Page, 1) - 1) * query.PageSize).Take(query.PageSize).Select(x => x.d).ToList(),
            };
            result.Facets["category"] = Facet(filtered.Select(d => d.Category));
            result.Facets["brand"] = Facet(filtered.Select(d => d.Brand));
            result.Facets["colour"] = Facet(filtered.SelectMany(d => d.Colours));
            result.Facets["size"] = Facet(filtered.SelectMany(d => d.Sizes));
            return Task.FromResult(result);
        }

        public Task<bool> PingAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public int Count => _documents.Count;

        private static int Score(ProductDocument d, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 1;
            var score = 0;
            foreach (var term in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Eq(d.StyleCode, term) || d.Eans.Contains(term) || d.Skus.Any(s => Eq(s, term))) score += 10;
                if (Contains(d.Name, term)) score += 5;
                if (Contains(d.Brand, term)) score += 3;
                if (d.Colours.Any(c => Contains(c, term))) score += 2;
                if (Contains(d.Description, term) || Contains(d.CategoryPath, term)) score += 1;
            }
            return score;
        }

        private static Dictionary<string, long> Facet(IEnumerable<string> values) =>
            values.Where(v => v != null).GroupBy(v => v).ToDictionary(g => g.Key, g => (long)g.Count());

        private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static bool Contains(string haystack, string needle) =>
            haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
