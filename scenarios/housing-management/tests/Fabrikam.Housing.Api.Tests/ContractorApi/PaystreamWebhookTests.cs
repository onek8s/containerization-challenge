using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Fabrikam.Housing.Api.Tests.Support;
using Fabrikam.Housing.Integration.Payments;
using Fabrikam.Housing.Rent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fabrikam.Housing.Api.Tests.ContractorApi
{
    public class PaystreamWebhookTests : IClassFixture<ContractorApiFactory>
    {
        private readonly ContractorApiFactory _factory;

        public PaystreamWebhookTests(ContractorApiFactory factory)
        {
            _factory = factory;
            _factory.CreateClient().GetAsync("/health").GetAwaiter().GetResult();
        }

        private static HttpRequestMessage Signed(string body, string secret)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/paystream") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add(PaystreamWebhookSignature.HeaderName, PaystreamWebhookSignature.Compute(body, secret));
            return request;
        }

        [Fact]
        public async Task Bad_signature_is_rejected()
        {
            var body = "{\"events\":[]}";
            var response = await _factory.CreateClient().SendAsync(Signed(body, "not-the-secret"));
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Failed_payment_posts_a_returned_direct_debit_once()
        {
            string paymentId;
            int tenancyId;
            using (var db = _factory.CreateDbContext())
            {
                var payment = await db.DirectDebitPayments.Include(p => p.Mandate)
                    .Where(p => p.Status == PaymentStatus.PaidOut).OrderByDescending(p => p.ChargeDate).FirstAsync();
                paymentId = payment.ProviderPaymentId;
                tenancyId = payment.Mandate.TenancyId;
            }

            var body = "{\"events\":[{\"id\":\"EVTEST0001\",\"created_at\":\"2021-06-14T09:00:00Z\",\"resource_type\":\"payments\",\"action\":\"charged_back\","
                + "\"links\":{\"payment\":\"" + paymentId + "\"},\"details\":{\"cause\":\"payer_disputed\",\"description\":\"The customer disputed the payment.\"}}]}";
            var client = _factory.CreateClient();
            (await client.SendAsync(Signed(body, ContractorApiFactory.WebhookSecret))).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await client.SendAsync(Signed(body, ContractorApiFactory.WebhookSecret))).StatusCode.Should().Be(HttpStatusCode.NoContent);

            using (var db = _factory.CreateDbContext())
            {
                var returned = await db.RentTransactions.Where(t => t.TenancyId == tenancyId && t.Reference == paymentId && t.Type == TransactionType.DirectDebitReturned).ToListAsync();
                returned.Should().HaveCount(1);
                (await db.DirectDebitPayments.SingleAsync(p => p.ProviderPaymentId == paymentId)).Status.Should().Be(PaymentStatus.ChargedBack);
                (await db.ProcessedWebhooks.CountAsync(p => p.EventId == "EVTEST0001")).Should().Be(1);
            }
        }

        [Fact]
        public async Task Cancelled_mandate_stops_collections()
        {
            string mandateId;
            using (var db = _factory.CreateDbContext())
                mandateId = (await db.Mandates.FirstAsync(m => m.Status == MandateStatus.Active)).ProviderMandateId;

            var body = "{\"events\":[{\"id\":\"EVTEST0002\",\"created_at\":\"2021-06-14T09:05:00Z\",\"resource_type\":\"mandates\",\"action\":\"cancelled\","
                + "\"links\":{\"mandate\":\"" + mandateId + "\"},\"details\":{\"cause\":\"bank_account_closed\"}}]}";
            (await _factory.CreateClient().SendAsync(Signed(body, ContractorApiFactory.WebhookSecret))).StatusCode.Should().Be(HttpStatusCode.NoContent);

            using (var db = _factory.CreateDbContext())
            {
                var mandate = await db.Mandates.SingleAsync(m => m.ProviderMandateId == mandateId);
                mandate.Status.Should().Be(MandateStatus.Cancelled);
                mandate.NextCollectionDate.Should().BeNull();
                mandate.CancellationReason.Should().Be("bank_account_closed");
            }
        }
    }

    public class RateLimitTests : IDisposable
    {
        private readonly ContractorApiFactory _factory = new ContractorApiFactory { RequestsPerMinute = 3 };

        [Fact]
        public async Task Requests_over_the_limit_get_429()
        {
            var client = _factory.ClientFor("DUNDRY");
            var codes = new System.Collections.Generic.List<HttpStatusCode>();
            for (var i = 0; i < 5; i++)
                codes.Add((await client.GetAsync("/api/v1/schedule-of-rates")).StatusCode);
            codes.Should().Contain(HttpStatusCode.TooManyRequests);
            codes.First().Should().Be(HttpStatusCode.OK);
        }

        public void Dispose() => _factory.Dispose();
    }
}
