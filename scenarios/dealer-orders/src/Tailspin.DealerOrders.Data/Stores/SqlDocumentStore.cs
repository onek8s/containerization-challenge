using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Dapper;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Data.Stores
{
    /// <summary>Generated documents (quotes, confirmations, contracts, price lists) kept in dlr.Document.</summary>
    public class SqlDocumentStore
    {
        private readonly SqlConnectionFactory _connections;

        public SqlDocumentStore(SqlConnectionFactory connections)
        {
            _connections = connections;
        }

        public StoredDocument Add(StoredDocument document)
        {
            using (var sha = SHA256.Create())
                document.Sha256 = string.Concat(sha.ComputeHash(document.Content).Select(b => b.ToString("x2")));
            using (var c = _connections.Open())
            {
                document.Id = c.ExecuteScalar<int>(@"
INSERT dlr.Document (Kind, DealerId, QuoteId, OrderId, FileName, ContentType, Content, Sha256, CreatedUtc, CreatedBy)
OUTPUT inserted.Id
VALUES (@Kind, @DealerId, @QuoteId, @OrderId, @FileName, @ContentType, @Content, @Sha256, @CreatedUtc, @CreatedBy);", document);
            }
            return document;
        }

        public StoredDocument Get(int id)
        {
            using (var c = _connections.Open())
                return c.QueryFirstOrDefault<StoredDocument>("SELECT * FROM dlr.Document WHERE Id = @id", new { id });
        }

        /// <summary>Document list without content.</summary>
        public IList<StoredDocument> List(int? dealerId, int? quoteId, int? orderId, int max = 100)
        {
            using (var c = _connections.Open())
                return c.Query<StoredDocument>(@"
SELECT TOP (@max) Id, Kind, DealerId, QuoteId, OrderId, FileName, ContentType, Sha256, CreatedUtc, CreatedBy
FROM dlr.Document
WHERE (@dealerId IS NULL OR DealerId = @dealerId) AND (@quoteId IS NULL OR QuoteId = @quoteId) AND (@orderId IS NULL OR OrderId = @orderId)
ORDER BY Id DESC", new { dealerId, quoteId, orderId, max }).ToList();
        }

        public int Purge(DocumentKind kind, DateTime olderThanUtc)
        {
            using (var c = _connections.Open())
                return c.Execute("DELETE FROM dlr.Document WHERE Kind = @kind AND CreatedUtc < @olderThanUtc", new { kind, olderThanUtc }, commandTimeout: 600);
        }
    }
}
