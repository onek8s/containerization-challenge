using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductReturns.Api;
using ProductReturns.Api.Services;
using ProductReturns.Core;
using ProductReturns.Core.Api;
using ProductReturns.Data;
using Xunit;

namespace ProductReturns.Tests
{
    public class ApiTests : IDisposable
    {
        private const string PortalKey = "test-portal-key";

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly TestDatabase _database = new TestDatabase();
        private readonly WebApplicationFactory<Startup> _factory;

        public ApiTests()
        {
            _factory = new WebApplicationFactory<Startup>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["ConnectionStrings:Returns"] = _database.ConnectionString,
                        ["Database:Provider"] = "Sqlite",
                        ["CatalogCache:Folder"] = _database.Folder,
                        ["Security:PortalKeyHashes:0"] = ApiKeys.Hash(PortalKey)
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IBusinessClock>(new FixedClock());

                    // The test server has no connection level authentication; Windows callers are not tested here.
                    services.Configure<AuthenticationOptions>(o => o.SchemeMap[NegotiateDefaults.AuthenticationScheme].HandlerType = typeof(NoWindowsAuthHandler));
                });
            });
        }

        public void Dispose()
        {
            _factory.Dispose();
            _database.Dispose();
        }

        private HttpClient Portal(string user = "CORP\\tester")
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", PortalKey);
            client.DefaultRequestHeaders.Add("X-Returns-User", user);
            return client;
        }

        private HttpClient Scanner()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", SampleData.DevelopmentScannerKey);
            return client;
        }

        private async Task<string> CreateReturn(HttpClient client)
        {
            var response = await client.PostAsJsonAsync("api/returns", new CreateReturnRequest
            {
                OrderNumber = "so-1048213",
                Email = "Anna.deVries@example.com",
                Lines =
                {
                    new CreateReturnLine { LineNumber = 1, Quantity = 1, ReasonCode = "NO_LONGER_NEEDED" },
                    new CreateReturnLine { LineNumber = 2, Quantity = 2, ReasonCode = "changed_mind", Comment = "Colour" }
                }
            }, Json);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<CreateReturnResponse>(Json)).RmaNumber;
        }

        [Fact]
        public async Task Requires_a_known_caller()
        {
            var anonymous = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/returns")).StatusCode);

            var scanner = Scanner();
            Assert.Equal(HttpStatusCode.Forbidden, (await scanner.GetAsync("api/returns")).StatusCode);

            var wrong = _factory.CreateClient();
            wrong.DefaultRequestHeaders.Add("X-Api-Key", "nope");
            Assert.Equal(HttpStatusCode.Unauthorized, (await wrong.GetAsync("api/returns")).StatusCode);

            Assert.Equal(HttpStatusCode.Forbidden, (await Portal().GetAsync("api/receiving/R2410-000001-0")).StatusCode);
        }

        [Fact]
        public async Task Order_lookup_checks_email_and_policy()
        {
            var client = Portal();
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("api/orders/SO-1048213?email=someone@example.com")).StatusCode);

            var order = await client.GetFromJsonAsync<OrderLookupResponse>("api/orders/SO-1048213?email=anna.devries@example.com", Json);
            Assert.Equal(3, order.Lines.Count);
            Assert.True(order.Lines[0].IsReturnable);
            Assert.False(order.Lines[2].IsReturnable);

            var old = await client.GetFromJsonAsync<OrderLookupResponse>("api/orders/SO-1047002?email=anna.devries@example.com", Json);
            Assert.All(old.Lines, l => Assert.False(l.IsReturnable));
        }

        [Fact]
        public async Task Rejects_lines_that_cannot_be_returned()
        {
            var response = await Portal().PostAsJsonAsync("api/returns", new CreateReturnRequest
            {
                OrderNumber = "SO-1048213",
                Email = "anna.devries@example.com",
                Lines = { new CreateReturnLine { LineNumber = 3, Quantity = 1, ReasonCode = "CHANGED_MIND" } }
            }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("cannot be returned", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Full_return_flow_from_request_to_refund()
        {
            var client = Portal("CORP\\agent1");
            var rma = await CreateReturn(client);

            var again = await client.PostAsJsonAsync("api/returns", new CreateReturnRequest
            {
                OrderNumber = "SO-1048213",
                Email = "anna.devries@example.com",
                Lines = { new CreateReturnLine { LineNumber = 1, Quantity = 1, ReasonCode = "DEFECTIVE" } }
            }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

            var approved = await (await client.PostAsync($"api/returns/{rma}/approve", null)).Content.ReadFromJsonAsync<ReturnDetail>(Json);
            Assert.Equal(ReturnStatus.Approved, approved.Status);
            Assert.Equal("CORP\\agent1", approved.DecidedBy);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"api/returns/{rma}/approve", null)).StatusCode);

            var scanner = Scanner();
            Assert.Equal(HttpStatusCode.BadRequest, (await scanner.GetAsync("api/receiving/R0000-000000-1")).StatusCode);
            var expected = await scanner.GetFromJsonAsync<ScanResponse>($"api/receiving/{rma.Replace("-", string.Empty)}", Json);
            Assert.True(expected.Accepted);

            var scan1 = await scanner.PostAsJsonAsync($"api/receiving/{rma}/scan", new ScanRequest { Sku = "WWI-10544", Quantity = 1, Condition = ItemCondition.Opened }, Json);
            Assert.Equal(HttpStatusCode.OK, scan1.StatusCode);
            var wrongSku = await scanner.PostAsJsonAsync($"api/receiving/{rma}/scan", new ScanRequest { Sku = "WWI-99999", Quantity = 1, Condition = ItemCondition.AsNew }, Json);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongSku.StatusCode);
            var scan2 = await (await scanner.PostAsJsonAsync($"api/receiving/{rma}/scan", new ScanRequest { Sku = "WWI-30114", Quantity = 2, Condition = ItemCondition.AsNew }, Json))
                .Content.ReadFromJsonAsync<ScanResponse>(Json);
            Assert.True(scan2.Complete);

            var inspected = await (await client.PostAsync($"api/returns/{rma}/complete-inspection", null)).Content.ReadFromJsonAsync<ReturnDetail>(Json);
            Assert.Equal(ReturnStatus.Inspected, inspected.Status);
            Assert.Equal(37.35m, inspected.RestockingFee);
            Assert.Equal(5.95m, inspected.LabelFee);
            Assert.Equal(249m + 49.90m - 37.35m - 5.95m, inspected.RefundAmount);

            var customer = await client.GetFromJsonAsync<ReturnDetail>($"api/returns/{rma}?email=anna.devries@example.com", Json);
            Assert.Empty(customer.Notes);
            Assert.Null(customer.DecidedBy);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/returns/{rma}?email=x@example.com")).StatusCode);

            var staff = await client.GetFromJsonAsync<ReturnDetail>($"api/returns/{rma}", Json);
            Assert.True(staff.Notes.Count >= 4);

            var search = await client.GetFromJsonAsync<ReturnSearchResponse>("api/returns?status=Inspected", Json);
            Assert.Equal(rma, search.Items.Single().RmaNumber);
        }

        [Fact]
        public async Task Customer_can_cancel_before_receipt()
        {
            var client = Portal();
            var rma = await CreateReturn(client);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"api/returns/{rma}/cancel?email=x@example.com", null)).StatusCode);
            var cancelled = await (await client.PostAsync($"api/returns/{rma}/cancel?email=anna.devries@example.com", null)).Content.ReadFromJsonAsync<ReturnDetail>(Json);
            Assert.Equal(ReturnStatus.Cancelled, cancelled.Status);

            var order = await client.GetFromJsonAsync<OrderLookupResponse>("api/orders/SO-1048213?email=anna.devries@example.com", Json);
            Assert.Equal(1, order.Lines[0].ReturnableQuantity);
        }

        private class NoWindowsAuthHandler : AuthenticationHandler<NegotiateOptions>
        {
            public NoWindowsAuthHandler(IOptionsMonitor<NegotiateOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
                : base(options, logger, encoder, clock)
            {
            }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
        }

        private class FixedClock : IBusinessClock
        {
            public DateTime UtcNow => DateTime.UtcNow;

            public DateTime Today => DateTime.Today;
        }
    }
}
