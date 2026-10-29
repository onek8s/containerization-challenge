using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Nest;

namespace VanArsdel.Catalog.Api.Search
{
    public class ElasticSearchOptions
    {
        public string Nodes { get; set; } = "http://search01.corp.local:9200";
        public string IndexName { get; set; } = "vanarsdel-products";
        public string Username { get; set; }
        public string Password { get; set; }
        public string CertificateFingerprint { get; set; }
    }

    /// <summary>Product search on the Elasticsearch 7 cluster (search01).</summary>
    public class ElasticProductSearch : IProductSearch
    {
        private readonly IElasticClient _client;
        private readonly string _index;
        private readonly ILogger<ElasticProductSearch> _logger;
        private int _indexChecked;

        public ElasticProductSearch(ElasticSearchOptions options, ILogger<ElasticProductSearch> logger)
        {
            _index = options.IndexName;
            _logger = logger;

            var nodes = options.Nodes.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(n => new Uri(n.Trim()));
            var settings = new ConnectionSettings(new StaticConnectionPool(nodes))
                .DefaultIndex(_index)
                .DefaultMappingFor<ProductDocument>(m => m.IdProperty(p => p.Id))
                .RequestTimeout(TimeSpan.FromSeconds(10))
                .ThrowExceptions(false);
            if (!string.IsNullOrEmpty(options.Username))
            {
                settings = settings.BasicAuthentication(options.Username, options.Password);
            }
            if (!string.IsNullOrEmpty(options.CertificateFingerprint))
            {
                settings = settings.CertificateFingerprint(options.CertificateFingerprint);
            }
            _client = new ElasticClient(settings);
        }

        public async Task IndexAsync(ProductDocument document, CancellationToken cancellationToken)
        {
            await EnsureIndexAsync(cancellationToken);
            var response = await _client.IndexDocumentAsync(document, cancellationToken);
            if (!response.IsValid) _logger.LogWarning("Indexing {StyleCode} failed: {Error}", document.StyleCode, response.DebugInformation);
        }

        public async Task IndexManyAsync(IReadOnlyCollection<ProductDocument> documents, CancellationToken cancellationToken)
        {
            await EnsureIndexAsync(cancellationToken);
            foreach (var batch in documents.Select((d, i) => new { d, i }).GroupBy(x => x.i / 500, x => x.d))
            {
                var response = await _client.IndexManyAsync(batch, _index, cancellationToken);
                if (response.Errors) _logger.LogWarning("Bulk index had {Count} errors", response.ItemsWithErrors.Count());
            }
        }

        public async Task RemoveAsync(string id, CancellationToken cancellationToken)
        {
            await _client.DeleteAsync<ProductDocument>(id, d => d.Index(_index), cancellationToken);
        }

        public async Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken)
        {
            var filters = new List<Func<QueryContainerDescriptor<ProductDocument>, QueryContainer>>();
            if (!query.IncludeDrafts) filters.Add(f => f.Term(t => t.Field(p => p.Status.Suffix("keyword")).Value("Published")));
            if (!string.IsNullOrEmpty(query.Category)) filters.Add(f => f.Term(t => t.Field(p => p.Category.Suffix("keyword")).Value(query.Category)));
            if (!string.IsNullOrEmpty(query.Brand)) filters.Add(f => f.Term(t => t.Field(p => p.Brand.Suffix("keyword")).Value(query.Brand)));
            if (!string.IsNullOrEmpty(query.Season)) filters.Add(f => f.Term(t => t.Field(p => p.Season.Suffix("keyword")).Value(query.Season)));
            if (!string.IsNullOrEmpty(query.Colour)) filters.Add(f => f.Term(t => t.Field(p => p.Colours.Suffix("keyword")).Value(query.Colour)));
            if (!string.IsNullOrEmpty(query.Size)) filters.Add(f => f.Term(t => t.Field(p => p.Sizes.Suffix("keyword")).Value(query.Size)));

            var response = await _client.SearchAsync<ProductDocument>(s => s
                .Index(_index)
                .From((Math.Max(query.Page, 1) - 1) * query.PageSize)
                .Size(query.PageSize)
                .Query(q => q.Bool(b => b
                    .Must(m => string.IsNullOrWhiteSpace(query.Text)
                        ? m.MatchAll()
                        : m.MultiMatch(mm => mm
                            .Query(query.Text)
                            .Fields(f => f.Field(p => p.Name, 3).Field(p => p.StyleCode, 5).Field(p => p.Brand, 2)
                                .Field(p => p.Description).Field(p => p.Skus).Field(p => p.Eans, 5).Field(p => p.Colours))
                            .Fuzziness(Fuzziness.Auto)))
                    .Filter(filters)))
                .Aggregations(a => a
                    .Terms("category", t => t.Field(p => p.Category.Suffix("keyword")).Size(50))
                    .Terms("brand", t => t.Field(p => p.Brand.Suffix("keyword")).Size(50))
                    .Terms("colour", t => t.Field(p => p.Colours.Suffix("keyword")).Size(50))
                    .Terms("size", t => t.Field(p => p.Sizes.Suffix("keyword")).Size(50))), cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogError("Search failed: {Error}", response.DebugInformation);
                throw new InvalidOperationException("Search is not available.");
            }

            var result = new ProductSearchResult { Total = response.Total, Items = response.Documents.ToList(), Engine = "elasticsearch" };
            foreach (var name in new[] { "category", "brand", "colour", "size" })
            {
                var terms = response.Aggregations.Terms(name);
                if (terms != null) result.Facets[name] = terms.Buckets.ToDictionary(b => b.Key, b => b.DocCount ?? 0);
            }
            return result;
        }

        public async Task<bool> PingAsync(CancellationToken cancellationToken)
        {
            var response = await _client.PingAsync(ct: cancellationToken);
            return response.IsValid;
        }

        private async Task EnsureIndexAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _indexChecked, 1, 0) != 0) return;
            var exists = await _client.Indices.ExistsAsync(_index, ct: cancellationToken);
            if (exists.Exists) return;

            var created = await _client.Indices.CreateAsync(_index, c => c
                .Settings(s => s.NumberOfShards(1).NumberOfReplicas(1)
                    .Analysis(an => an.Analyzers(az => az.Custom("product_text", ca => ca.Tokenizer("standard").Filters("lowercase", "asciifolding")))))
                .Map<ProductDocument>(m => m.AutoMap()
                    .Properties(p => p
                        .Text(t => t.Name(n => n.Name).Analyzer("product_text").Fields(f => f.Keyword(k => k.Name("keyword"))))
                        .Text(t => t.Name(n => n.Description).Analyzer("product_text")))), cancellationToken);
            if (!created.IsValid) _logger.LogWarning("Could not create index {Index}: {Error}", _index, created.DebugInformation);
        }
    }
}
