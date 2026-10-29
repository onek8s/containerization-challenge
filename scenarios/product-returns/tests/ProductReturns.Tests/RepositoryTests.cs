using System;
using System.Linq;
using System.Threading.Tasks;
using ProductReturns.Core;
using ProductReturns.Data;
using Xunit;

namespace ProductReturns.Tests
{
    public class RepositoryTests
    {
        [Fact]
        public async Task Create_assigns_rma_and_counts_returned_quantities()
        {
            using var database = new TestDatabase();
            using (var db = database.CreateContext())
            {
                var repository = new ReturnsRepository(db);
                var request = new ReturnRequest { OrderNumber = "SO-1048213", CustomerEmail = "a@example.com", CreatedUtc = DateTime.UtcNow };
                request.Lines.Add(new ReturnLine { OrderLineNumber = 2, Sku = "WWI-30114", Quantity = 1, UnitPrice = 24.95m, ReasonCode = "DAMAGED" });
                await repository.CreateAsync(request);
                Assert.True(RmaNumber.IsValid(request.RmaNumber));

                var rejected = new ReturnRequest { OrderNumber = "SO-1048213", CustomerEmail = "a@example.com", CreatedUtc = DateTime.UtcNow, Status = ReturnStatus.Rejected };
                rejected.Lines.Add(new ReturnLine { OrderLineNumber = 2, Sku = "WWI-30114", Quantity = 1, UnitPrice = 24.95m, ReasonCode = "DAMAGED" });
                await repository.CreateAsync(rejected);
            }

            using (var db = database.CreateContext())
            {
                var repository = new ReturnsRepository(db);
                var returned = await repository.ReturnedQuantitiesAsync("SO-1048213");
                Assert.Equal(1, returned[2]);
                Assert.False(returned.ContainsKey(1));

                var found = await repository.GetAsync(RmaNumber.Create(1, DateTime.UtcNow).Replace("-", string.Empty).ToLowerInvariant());
                Assert.NotNull(found);
                Assert.Single(found.Lines);

                var page = await repository.SearchAsync(new ReturnSearch { Text = "SO-1048213" });
                Assert.Equal(2, page.Total);
                var counts = await repository.CountByStatusAsync();
                Assert.Equal(1, counts[ReturnStatus.Rejected]);
            }
        }

        [Fact]
        public async Task Scanner_keys_are_stored_hashed()
        {
            using var database = new TestDatabase();
            using var db = database.CreateContext();
            var repository = new ReturnsRepository(db);
            Assert.NotNull(await repository.FindScannerAsync(ApiKeys.Hash(SampleData.DevelopmentScannerKey)));
            Assert.Null(await repository.FindScannerAsync(ApiKeys.Hash("other")));
            Assert.DoesNotContain(db.Scanners, s => s.ApiKeyHash == SampleData.DevelopmentScannerKey);
            Assert.Equal(64, db.Scanners.First().ApiKeyHash.Length);
        }
    }
}
