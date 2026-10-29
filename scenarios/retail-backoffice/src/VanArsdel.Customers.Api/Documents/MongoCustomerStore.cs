using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace VanArsdel.Customers.Api.Documents
{
    public class MongoCustomerStore : ICustomerStore
    {
        private readonly IMongoCollection<CustomerDocument> _customers;

        static MongoCustomerStore()
        {
            ConventionRegistry.Register("vanarsdel", new ConventionPack { new IgnoreExtraElementsConvention(true), new IgnoreIfNullConvention(true) }, _ => true);
            BsonClassMap.RegisterClassMap<CustomerDocument>(m =>
            {
                m.AutoMap();
                m.MapIdMember(c => c.Id);
            });
        }

        public MongoCustomerStore(string connectionString, string database)
        {
            var client = new MongoClient(connectionString);
            _customers = client.GetDatabase(database).GetCollection<CustomerDocument>("customers");
            _customers.Indexes.CreateMany(new[]
            {
                new CreateIndexModel<CustomerDocument>(Builders<CustomerDocument>.IndexKeys.Ascending(c => c.Email), new CreateIndexOptions { Unique = true, Sparse = true }),
                new CreateIndexModel<CustomerDocument>(Builders<CustomerDocument>.IndexKeys.Ascending("Loyalty.CardNo"), new CreateIndexOptions { Sparse = true }),
                new CreateIndexModel<CustomerDocument>(Builders<CustomerDocument>.IndexKeys.Ascending(c => c.LastName)),
            });
        }

        public async Task<CustomerDocument> GetAsync(string id, CancellationToken ct) =>
            await _customers.Find(c => c.Id == id).FirstOrDefaultAsync(ct);

        public async Task<CustomerDocument> FindByEmailAsync(string email, CancellationToken ct) =>
            await _customers.Find(c => c.Email == email).FirstOrDefaultAsync(ct);

        public async Task<CustomerDocument> FindByCardAsync(string cardNo, CancellationToken ct) =>
            await _customers.Find(Builders<CustomerDocument>.Filter.Eq("Loyalty.CardNo", cardNo)).FirstOrDefaultAsync(ct);

        public async Task<IReadOnlyList<CustomerDocument>> SearchAsync(string text, int take, CancellationToken ct)
        {
            var regex = new BsonRegularExpression("^" + Regex.Escape(text ?? string.Empty), "i");
            var filter = Builders<CustomerDocument>.Filter.Or(
                Builders<CustomerDocument>.Filter.Regex(c => c.Email, regex),
                Builders<CustomerDocument>.Filter.Regex(c => c.LastName, regex),
                Builders<CustomerDocument>.Filter.Regex(c => c.Phone, regex),
                Builders<CustomerDocument>.Filter.Regex("Loyalty.CardNo", regex));
            return await _customers.Find(filter).Limit(take).ToListAsync(ct);
        }

        public async Task<IReadOnlyList<CustomerDocument>> PageAsync(int skip, int take, CancellationToken ct) =>
            await _customers.Find(FilterDefinition<CustomerDocument>.Empty).SortBy(c => c.Id).Skip(skip).Limit(take).ToListAsync(ct);

        public Task<long> CountAsync(CancellationToken ct) => _customers.CountDocumentsAsync(FilterDefinition<CustomerDocument>.Empty, cancellationToken: ct);

        public async Task<bool> SaveAsync(CustomerDocument document, CancellationToken ct)
        {
            var expected = document.Version;
            document.Version++;
            if (expected == 0)
            {
                try
                {
                    await _customers.InsertOneAsync(document, cancellationToken: ct);
                    return true;
                }
                catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    document.Version = expected;
                    return false;
                }
            }
            var result = await _customers.ReplaceOneAsync(c => c.Id == document.Id && c.Version == expected, document, cancellationToken: ct);
            if (result.ModifiedCount == 1) return true;
            document.Version = expected;
            return false;
        }

        public async Task<bool> PingAsync(CancellationToken ct)
        {
            try
            {
                await _customers.Database.RunCommandAsync((Command<BsonDocument>)"{ping:1}", cancellationToken: ct);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
