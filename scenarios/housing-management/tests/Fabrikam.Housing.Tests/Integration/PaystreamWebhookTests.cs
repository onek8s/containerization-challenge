using System;
using Fabrikam.Housing.Integration.Payments;
using FluentAssertions;
using Xunit;

namespace Fabrikam.Housing.Tests.Integration
{
    public class PaystreamWebhookTests
    {
        private const string Secret = "whsec_test_8f2b";
        private const string Body = @"{""events"":[
 {""id"":""EV0012"",""created_at"":""2021-04-12T09:15:00.000Z"",""resource_type"":""payments"",""action"":""failed"",""links"":{""payment"":""PM00451""},""details"":{""cause"":""insufficient_funds"",""description"":""The customer's bank refused the payment.""}},
 {""id"":""EV0013"",""created_at"":""2021-04-12T09:16:00.000Z"",""resource_type"":""mandates"",""action"":""cancelled"",""links"":{""mandate"":""MD0007""},""details"":{""cause"":""bank_account_closed""}}
]}";

        [Fact]
        public void Valid_signature_is_accepted()
        {
            var signature = PaystreamWebhookSignature.Compute(Body, Secret);
            signature.Should().MatchRegex("^[0-9a-f]{64}$");
            PaystreamWebhookSignature.Verify(Body, signature, Secret).Should().BeTrue();
            PaystreamWebhookSignature.Verify(Body, signature.ToUpperInvariant(), Secret).Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("abc")]
        [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
        public void Wrong_or_missing_signature_is_rejected(string signature)
        {
            PaystreamWebhookSignature.Verify(Body, signature, Secret).Should().BeFalse();
        }

        [Fact]
        public void Tampered_body_is_rejected()
        {
            var signature = PaystreamWebhookSignature.Compute(Body, Secret);
            PaystreamWebhookSignature.Verify(Body.Replace("PM00451", "PM00452"), signature, Secret).Should().BeFalse();
            PaystreamWebhookSignature.Verify(Body, signature, "other").Should().BeFalse();
        }

        [Fact]
        public void Parser_reads_events_and_resource_ids()
        {
            var events = PaystreamWebhookParser.Parse(Body);
            events.Should().HaveCount(2);
            events[0].Id.Should().Be("EV0012");
            events[0].ResourceType.Should().Be("payments");
            events[0].Action.Should().Be("failed");
            events[0].ResourceId.Should().Be("PM00451");
            events[0].Cause.Should().Be("insufficient_funds");
            events[0].CreatedAt.ToUniversalTime().Should().Be(new DateTime(2021, 4, 12, 9, 15, 0, DateTimeKind.Utc));
            events[1].ResourceId.Should().Be("MD0007");
            events[1].Description.Should().BeNull();
        }

        [Fact]
        public void Empty_body_gives_no_events()
        {
            PaystreamWebhookParser.Parse("{}").Should().BeEmpty();
        }
    }
}
