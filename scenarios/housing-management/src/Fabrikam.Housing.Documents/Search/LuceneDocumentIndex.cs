using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Directory = Lucene.Net.Store.Directory;

namespace Fabrikam.Housing.Documents.Search
{
    /// <summary>
    /// Full text index of the document store, kept in a folder on the document server. One writer per
    /// process; searches use a reader reopened after each commit.
    /// </summary>
    public class LuceneDocumentIndex : IDocumentIndex
    {
        private const LuceneVersion Version = LuceneVersion.LUCENE_48;
        private readonly Directory _directory;
        private readonly StandardAnalyzer _analyzer;
        private readonly IndexWriter _writer;
        private readonly SearcherManager _searchers;

        public LuceneDocumentIndex(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            _directory = FSDirectory.Open(new DirectoryInfo(folder));
            _analyzer = new StandardAnalyzer(Version);
            _writer = new IndexWriter(_directory, new IndexWriterConfig(Version, _analyzer) { OpenMode = OpenMode.CREATE_OR_APPEND });
            _searchers = new SearcherManager(_writer, true, null);
        }

        public void Add(IndexedDocument document)
        {
            var key = document.DocumentKey.ToString("N");
            var doc = new Document
            {
                new StringField("key", key, Field.Store.YES),
                new TextField("title", document.Title ?? "", Field.Store.YES),
                new StringField("category", document.Category ?? "", Field.Store.YES),
                new TextField("text", document.Text ?? "", Field.Store.YES),
                new StringField("tenancy", document.TenancyId?.ToString(CultureInfo.InvariantCulture) ?? "", Field.Store.YES),
                new StringField("property", document.PropertyId?.ToString(CultureInfo.InvariantCulture) ?? "", Field.Store.YES),
                new StringField("uploaded", document.UploadedAt.ToString("o", CultureInfo.InvariantCulture), Field.Store.YES)
            };
            _writer.UpdateDocument(new Term("key", key), doc);
        }

        public void Delete(Guid documentKey)
        {
            _writer.DeleteDocuments(new Term("key", documentKey.ToString("N")));
        }

        public void Commit()
        {
            _writer.Commit();
            _searchers.MaybeRefresh();
        }

        public IList<IndexHit> Search(string query, int? tenancyId, int? propertyId, int max)
        {
            var bool_ = new BooleanQuery();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var parser = new MultiFieldQueryParser(Version, new[] { "title", "text" }, _analyzer) { DefaultOperator = Operator.AND };
                Query parsed;
                try
                {
                    parsed = parser.Parse(query);
                }
                catch (ParseException)
                {
                    // Treat what the user typed as plain words (lower case so AND/OR/NOT are not operators).
                    parsed = parser.Parse(QueryParserBase.Escape(query.ToLowerInvariant()));
                }
                bool_.Add(parsed, Occur.MUST);
            }
            else
            {
                bool_.Add(new MatchAllDocsQuery(), Occur.MUST);
            }
            if (tenancyId.HasValue) bool_.Add(new TermQuery(new Term("tenancy", tenancyId.Value.ToString(CultureInfo.InvariantCulture))), Occur.MUST);
            if (propertyId.HasValue) bool_.Add(new TermQuery(new Term("property", propertyId.Value.ToString(CultureInfo.InvariantCulture))), Occur.MUST);

            _searchers.MaybeRefresh();
            var searcher = _searchers.Acquire();
            try
            {
                var top = searcher.Search(bool_, Math.Max(1, max));
                var hits = new List<IndexHit>();
                foreach (var sd in top.ScoreDocs)
                {
                    var d = searcher.Doc(sd.Doc);
                    hits.Add(new IndexHit
                    {
                        DocumentKey = Guid.ParseExact(d.Get("key"), "N"),
                        Title = d.Get("title"),
                        Category = d.Get("category"),
                        Snippet = Snippet(d.Get("text"), query),
                        Score = float.IsNaN(sd.Score) ? 0 : sd.Score,
                        TenancyId = int.TryParse(d.Get("tenancy"), out var t) ? t : (int?)null,
                        PropertyId = int.TryParse(d.Get("property"), out var p) ? p : (int?)null,
                        UploadedAt = DateTime.Parse(d.Get("uploaded"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                    });
                }
                return hits;
            }
            finally
            {
                _searchers.Release(searcher);
            }
        }

        /// <summary>Up to 200 characters around the first query word found in the text.</summary>
        public static string Snippet(string text, string query)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var at = -1;
            foreach (var word in (query ?? "").Split(new[] { ' ', '"', '*', '+', '-' }, StringSplitOptions.RemoveEmptyEntries))
            {
                at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                if (at >= 0) break;
            }
            var start = Math.Max(0, at < 0 ? 0 : at - 60);
            var length = Math.Min(200, text.Length - start);
            return (start > 0 ? "..." : "") + text.Substring(start, length) + (start + length < text.Length ? "..." : "");
        }

        public void Dispose()
        {
            _searchers.Dispose();
            _writer.Dispose();
            _analyzer.Dispose();
            _directory.Dispose();
        }
    }
}
