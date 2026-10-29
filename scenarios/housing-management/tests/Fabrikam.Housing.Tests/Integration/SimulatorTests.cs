using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Integration.Messaging;
using Fabrikam.Housing.Integration.Simulated;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Xunit;

namespace Fabrikam.Housing.Tests.Integration
{
    public class SimulatorTests : IClassFixture<SimulatorFolderFixture>
    {
        private readonly SimulatorFolderFixture _fixture;

        public SimulatorTests(SimulatorFolderFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Mandate_flow_redirects_back_and_creates_an_active_mandate()
        {
            var gateway = new SimulatedPaymentsGateway(_fixture.Options(), NullLogger<SimulatedPaymentsGateway>.Instance);
            var flow = await gateway.StartMandateAsync("T020001", "Rent", "a@example.com", "http://localhost:5101/DirectDebit/Complete?x=1", "sess-1");
            flow.RedirectUrl.Should().Be("http://localhost:5101/DirectDebit/Complete?x=1&redirect_flow_id=" + flow.FlowId);

            var mandate = await gateway.CompleteMandateAsync(flow.FlowId, "sess-1");
            mandate.MandateId.Should().StartWith("MD");
            (await gateway.CompleteMandateAsync(flow.FlowId, "sess-1")).MandateId.Should().Be(mandate.MandateId);
            Func<Task> wrongSession = () => gateway.CompleteMandateAsync(flow.FlowId, "other");
            await wrongSession.Should().ThrowAsync<Fabrikam.Housing.Integration.IntegrationException>();

            var events = await gateway.ListEventsAsync(DateTime.Now.AddMinutes(-5));
            events.Should().Contain(e => e.ResourceType == "mandates" && e.Action == "active" && e.ResourceId == mandate.MandateId);
        }

        [Fact]
        public async Task Payments_are_idempotent_and_settle_after_the_charge_date()
        {
            var gateway = new SimulatedPaymentsGateway(_fixture.Options(), NullLogger<SimulatedPaymentsGateway>.Instance);
            var flow = await gateway.StartMandateAsync("T020002", "Rent", null, "http://localhost/ok", "s2");
            var mandate = await gateway.CompleteMandateAsync(flow.FlowId, "s2");

            var since = DateTime.Now.AddSeconds(-1);
            var ok = await gateway.CreatePaymentAsync(mandate.MandateId, 412.50m, DateTime.Today, "FABRIKAM T020002", "key-1");
            (await gateway.CreatePaymentAsync(mandate.MandateId, 412.50m, DateTime.Today, "FABRIKAM T020002", "key-1")).Should().Be(ok);
            var bad = await gateway.CreatePaymentAsync(mandate.MandateId, 9.70m, DateTime.Today, "FABRIKAM T020002", "key-2");
            var later = await gateway.CreatePaymentAsync(mandate.MandateId, 100m, DateTime.Today.AddDays(7), "FABRIKAM T020002", "key-3");

            var events = await gateway.ListEventsAsync(since);
            events.Should().Contain(e => e.ResourceId == ok && e.Action == "confirmed");
            events.Should().Contain(e => e.ResourceId == bad && e.Action == "failed" && e.Cause == "insufficient_funds");
            events.Should().NotContain(e => e.ResourceId == later && e.Action != "created");

            await gateway.CancelMandateAsync(mandate.MandateId);
            Func<Task> afterCancel = () => gateway.CreatePaymentAsync(mandate.MandateId, 1m, DateTime.Today, "x", "key-4");
            await afterCancel.Should().ThrowAsync<Fabrikam.Housing.Integration.IntegrationException>();
        }

        [Fact]
        public async Task Card_payment_session_returns_to_the_caller_and_succeeds()
        {
            var gateway = new SimulatedPaymentsGateway(_fixture.Options(), NullLogger<SimulatedPaymentsGateway>.Instance);
            var session = await gateway.StartCardPaymentAsync("T020003", 55.25m, "http://localhost:5101/Rent/PaymentComplete", "t@example.com");
            session.PaymentUrl.Should().EndWith("?session_id=" + session.SessionId);
            var result = await gateway.GetCardPaymentAsync(session.SessionId);
            result.Status.Should().Be("succeeded");
            result.Amount.Should().Be(55.25m);
            result.PaymentId.Should().StartWith("CP");
            (await gateway.GetCardPaymentAsync(session.SessionId)).PaymentId.Should().Be(result.PaymentId);
            (await gateway.GetCardPaymentAsync("CS-unknown")).Should().BeNull();
        }

        [Fact]
        public async Task Sms_simulator_writes_the_message()
        {
            var sms = new SimulatedSmsSender(_fixture.Options(), NullLogger<SimulatedSmsSender>.Instance);
            var id = await sms.SendAsync("07700 900123", "Your repair is booked");
            var file = Path.Combine(_fixture.Folder, "sms", id + ".txt");
            File.ReadAllText(file).Should().Contain("+447700900123").And.Contain("Your repair is booked");
        }

        [Theory]
        [InlineData("07700 900123", "+447700900123")]
        [InlineData("+447700900123", "+447700900123")]
        [InlineData("00447700900123", "+447700900123")]
        public void Mobile_numbers_are_converted_to_international_format(string input, string expected)
        {
            TextlineSmsSender.ToInternational(input).Should().Be(expected);
        }

        [Fact]
        public async Task Email_simulator_writes_an_eml_with_attachments()
        {
            var mail = new SimulatedEmailSender(_fixture.Options(), NullLogger<SimulatedEmailSender>.Instance);
            await mail.SendAsync("tenant@example.com", "Your rent statement", "<p>Attached</p>",
                new[] { new EmailAttachment { FileName = "statement.pdf", ContentType = "application/pdf", Content = new byte[] { 1, 2, 3 } } });
            var file = Directory.GetFiles(Path.Combine(_fixture.Folder, "mail"), "*tenant_example.com.eml").Single();
            var message = MimeMessage.Load(file);
            message.Subject.Should().Be("Your rent statement");
            message.To.Mailboxes.Single().Address.Should().Be("tenant@example.com");
            message.Attachments.OfType<MimePart>().Single().FileName.Should().Be("statement.pdf");
        }

        [Fact]
        public async Task Bacs_simulator_writes_the_file()
        {
            var bacs = new SimulatedBacsBureau(_fixture.Options(), NullLogger<SimulatedBacsBureau>.Instance);
            await bacs.UploadAsync("FAB00042.txt", "VOL1000042");
            File.ReadAllText(Path.Combine(_fixture.Folder, "bacs", "FAB00042.txt")).Should().Be("VOL1000042");
        }

        [Fact]
        public async Task Postcode_simulator_is_deterministic_and_rejects_invalid_postcodes()
        {
            var lookup = new SimulatedPostcodeLookup();
            var a = await lookup.LookupAsync("bs5 6xx");
            var b = await lookup.LookupAsync("BS56XX");
            a.Postcode.Should().Be("BS5 6XX");
            a.Latitude.Should().Be(b.Latitude);
            a.Latitude.Should().BeInRange(51.40, 51.50);
            a.Longitude.Should().BeInRange(-2.66, -2.52);
            (await lookup.LookupAsync("not a postcode")).Should().BeNull();
        }

        [Theory]
        [InlineData(51.4652, -2.5634, "EAST")]
        [InlineData(51.4153, -2.6170, "SOUTH")]
        [InlineData(51.4870, -2.5170, "NORTH")]
        [InlineData(52.4862, -1.8904, null)]
        public async Task Patch_simulator_maps_points_to_patches(double lat, double lon, string expected)
        {
            (await new SimulatedPatchLocator().FindPatchAsync(lat, lon)).Should().Be(expected);
        }

        [Fact]
        public void Patch_simulator_maps_postcodes()
        {
            SimulatedPatchLocator.ForPostcode("BS14 8AB").Should().Be("SOUTH");
            SimulatedPatchLocator.ForPostcode("BS1 6PL").Should().BeNull();
        }
    }
}
