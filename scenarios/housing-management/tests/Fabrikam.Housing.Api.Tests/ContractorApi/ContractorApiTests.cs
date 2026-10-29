using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Fabrikam.Housing.Api.Tests.Support;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Fabrikam.Housing.Api.Tests.ContractorApi
{
    public class ContractorApiTests : IClassFixture<ContractorApiFactory>
    {
        private readonly ContractorApiFactory _factory;

        public ContractorApiTests(ContractorApiFactory factory)
        {
            _factory = factory;
            // Starting the server seeds the development database.
            _factory.CreateClient().GetAsync("/health").GetAwaiter().GetResult();
        }

        private static StringContent Json(object value) =>
            new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json");

        private async Task<WorkOrder> NewAssignedOrder(string contractorCode, params (string Sor, decimal Qty)[] lines)
        {
            using (var db = _factory.CreateDbContext())
            {
                var contractor = await db.Contractors.SingleAsync(c => c.Code == contractorCode);
                var tenancy = await db.Tenancies.Include(t => t.Property).OrderBy(t => t.Id).Skip(new Random().Next(200)).FirstAsync();
                var counters = new CounterService(db, new ConnectionFactory("Sqlite", "Data Source=" + _factory.DatabasePath));
                var seq = await counters.NextAsync("WorkOrder2021");
                var order = new WorkOrder
                {
                    Reference = RepairRules.NextReference(2021, seq),
                    PropertyId = tenancy.PropertyId,
                    TenancyId = tenancy.Id,
                    Channel = ReportChannel.Phone,
                    ReportedBy = "test",
                    Description = "Test repair",
                    Trade = Trade.Plumbing,
                    Priority = RepairPriority.Routine,
                    Status = WorkOrderStatus.Assigned,
                    ReportedAt = ContractorApiFactory.Now.AddHours(-2),
                    TargetDate = ContractorApiFactory.Now.AddDays(20),
                    ContractorId = contractor.Id,
                    AssignedAt = ContractorApiFactory.Now.AddHours(-1),
                    CreatedAt = ContractorApiFactory.Now
                };
                foreach (var (sor, qty) in lines)
                {
                    var item = await db.ScheduleOfRates.SingleAsync(s => s.Code == sor);
                    order.Lines.Add(new WorkOrderLine { SorCode = sor, Description = item.Description, Quantity = qty, UnitPrice = item.UnitPrice });
                }
                db.WorkOrders.Add(order);
                await db.SaveChangesAsync();
                return order;
            }
        }

        [Fact]
        public async Task Anonymous_requests_are_rejected()
        {
            var response = await _factory.CreateClient().GetAsync("/api/v1/workorders");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Work_orders_are_scoped_to_the_calling_contractor()
        {
            var client = _factory.ClientFor("AVONPH");
            var response = await client.GetAsync("/api/v1/workorders?pageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var page = JObject.Parse(await response.Content.ReadAsStringAsync());
            var references = page["items"].Select(i => (string)i["reference"]).ToList();
            references.Should().NotBeEmpty();

            using (var db = _factory.CreateDbContext())
            {
                var avon = await db.Contractors.SingleAsync(c => c.Code == "AVONPH");
                var owners = await db.WorkOrders.Where(w => references.Contains(w.Reference)).Select(w => w.ContractorId).Distinct().ToListAsync();
                owners.Should().Equal(avon.Id);

                var other = await db.WorkOrders.Include(w => w.Contractor).FirstAsync(w => w.Contractor.Code == "BRISEL");
                (await client.GetAsync("/api/v1/workorders/" + other.Reference)).StatusCode.Should().Be(HttpStatusCode.NotFound);
                (await _factory.ClientFor("BRISEL").GetAsync("/api/v1/workorders/" + other.Reference)).StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        [Fact]
        public async Task Accept_book_complete_and_invoice()
        {
            var order = await NewAssignedOrder("AVONPH", ("PL0101", 1));
            var client = _factory.ClientFor("AVONPH");
            var url = "/api/v1/workorders/" + order.Reference;

            (await client.PostAsync(url + "/accept", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var saturday = await client.PostAsync(url + "/appointments", Json(new { date = "2021-06-19", slot = "Morning" }));
            saturday.StatusCode.Should().Be((HttpStatusCode)422);
            (await saturday.Content.ReadAsStringAsync()).Should().Contain("APPT_DAY");

            (await client.PostAsync(url + "/appointments", Json(new { date = "2021-06-16", slot = "Afternoon" }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var complete = await client.PostAsync(url + "/complete", Json(new
            {
                notes = "Renewed washer and reseated tap",
                labourHours = 1.5,
                extraLines = new[] { new { sorCode = "PL0305", quantity = 2 } }
            }));
            complete.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var detail = JObject.Parse(await (await client.GetAsync(url)).Content.ReadAsStringAsync());
            ((string)detail["status"]).Should().Be("Completed");
            detail["lines"].Should().HaveCount(2);
            detail["appointments"].Single(a => !(bool)a["cancelled"])["outcome"].ToString().Should().Be("Completed");

            // 38.50 + 2 x 46.10 = 130.70
            var invoice = await client.PostAsync("/api/v1/invoices", Json(new { invoiceNumber = "T-" + order.Reference, invoiceDate = "2021-06-14", workOrderReference = order.Reference, netAmount = 130.70m, vatAmount = 26.14m }));
            invoice.StatusCode.Should().Be(HttpStatusCode.Created);
            ((string)JObject.Parse(await invoice.Content.ReadAsStringAsync())["status"]).Should().Be("Received");

            var duplicate = await client.PostAsync("/api/v1/invoices", Json(new { invoiceNumber = "T-" + order.Reference, invoiceDate = "2021-06-14", workOrderReference = order.Reference, netAmount = 130.70m, vatAmount = 26.14m }));
            duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

            using (var db = _factory.CreateDbContext())
            {
                var saved = await db.WorkOrders.Include(w => w.Events).SingleAsync(w => w.Reference == order.Reference);
                saved.CompletedAt.Should().NotBeNull();
                saved.Events.Select(e => e.Actor).Should().Contain("contractor-avonph");
            }
        }

        [Fact]
        public async Task Invoice_over_tolerance_is_queried()
        {
            var order = await NewAssignedOrder("AVONPH", ("PL0210", 1));
            var client = _factory.ClientFor("AVONPH");
            var url = "/api/v1/workorders/" + order.Reference;
            (await client.PostAsync(url + "/accept", null)).EnsureSuccessStatusCode();
            (await client.PostAsync(url + "/complete", Json(new { notes = "Done", labourHours = 1 }))).EnsureSuccessStatusCode();

            var response = await client.PostAsync("/api/v1/invoices", Json(new { invoiceNumber = "Q-" + order.Reference, invoiceDate = "2021-06-14", workOrderReference = order.Reference, netAmount = 154.75m, vatAmount = 30.95m }));
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var body = JObject.Parse(await response.Content.ReadAsStringAsync());
            ((string)body["status"]).Should().Be("Queried");
            ((string)body["queryReason"]).Should().Contain("exceeds priced work");
        }

        [Fact]
        public async Task Reject_returns_the_job_to_triage()
        {
            var order = await NewAssignedOrder("AVONPH", ("PL0101", 1));
            var client = _factory.ClientFor("AVONPH");
            (await client.PostAsync("/api/v1/workorders/" + order.Reference + "/reject", Json(new { reason = "" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await client.PostAsync("/api/v1/workorders/" + order.Reference + "/reject", Json(new { reason = "No engineer in that area this week" }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
            using (var db = _factory.CreateDbContext())
            {
                var saved = await db.WorkOrders.SingleAsync(w => w.Reference == order.Reference);
                saved.Status.Should().Be(WorkOrderStatus.Triaged);
                saved.ContractorId.Should().BeNull();
            }
        }

        [Fact]
        public async Task Third_no_access_puts_the_job_on_hold()
        {
            var order = await NewAssignedOrder("AVONPH", ("PL0101", 1));
            var client = _factory.ClientFor("AVONPH");
            var url = "/api/v1/workorders/" + order.Reference;
            (await client.PostAsync(url + "/accept", null)).EnsureSuccessStatusCode();
            for (var i = 0; i < 3; i++)
            {
                (await client.PostAsync(url + "/appointments", Json(new { date = "2021-06-1" + (5 + i), slot = "Morning" }))).EnsureSuccessStatusCode();
                (await client.PostAsync(url + "/no-access", Json(new { outcome = "Card left" }))).EnsureSuccessStatusCode();
            }
            using (var db = _factory.CreateDbContext())
            {
                var saved = await db.WorkOrders.SingleAsync(w => w.Reference == order.Reference);
                saved.NoAccessCount.Should().Be(3);
                saved.Status.Should().Be(WorkOrderStatus.OnHold);
            }
        }

        [Fact]
        public async Task Gas_certificate_upload_supersedes_and_raises_remedial_work()
        {
            string propertyRef;
            int propertyId;
            using (var db = _factory.CreateDbContext())
            {
                var cert = await db.Certificates.Include(c => c.Property)
                    .FirstAsync(c => c.Type == ComplianceType.GasSafety && !c.Superseded && c.InspectedOn < ContractorApiFactory.Now.AddDays(-30));
                propertyRef = cert.Property.Reference;
                propertyId = cert.Property.Id;
            }

            var client = _factory.ClientFor("GASCAR");
            var form = new MultipartFormDataContent
            {
                { new StringContent(propertyRef), "propertyReference" },
                { new StringContent("GasSafety"), "type" },
                { new StringContent("CP12-T1"), "certificateNumber" },
                { new StringContent("2021-06-14"), "inspectedOn" },
                { new StringContent("RemedialRequired"), "outcome" },
                { new StringContent("Kieran Mensah"), "engineerName" },
                { new StringContent("123456"), "engineerRegistration" },
                { new StringContent("Gas fire flue spillage test failed (AR)"), "observations" }
            };
            var pdf = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n"));
            pdf.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            form.Add(pdf, "file", "CP12-T1.pdf");

            var response = await client.PostAsync("/api/v1/certificates", form);
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.Created, text);
            var body = JObject.Parse(text);
            ((string)body["remedialWorkOrder"]).Should().NotBeNullOrEmpty();
            ((DateTime)body["expiresOn"]).Should().Be(new DateTime(2022, 6, 13));

            using (var db = _factory.CreateDbContext())
            {
                var gas = await db.Certificates.Where(c => c.PropertyId == propertyId && c.Type == ComplianceType.GasSafety).ToListAsync();
                gas.Count(c => !c.Superseded).Should().Be(1);
                gas.Single(c => !c.Superseded).CertificateNumber.Should().Be("CP12-T1");
                var remedial = await db.WorkOrders.SingleAsync(w => w.Reference == (string)body["remedialWorkOrder"]);
                remedial.Priority.Should().Be(RepairPriority.Urgent);
            }
            _factory.Documents.Uploads.Should().Contain(u => u.Upload.Title.Contains("CP12-T1"));
        }

        [Fact]
        public async Task Gas_certificate_from_contractor_without_gas_safe_is_refused()
        {
            var client = _factory.ClientFor("BRISEL");
            var form = new MultipartFormDataContent
            {
                { new StringContent("P01000"), "propertyReference" },
                { new StringContent("GasSafety"), "type" },
                { new StringContent("CP12-X"), "certificateNumber" },
                { new StringContent("2021-06-14"), "inspectedOn" },
                { new StringContent("Satisfactory"), "outcome" },
                { new StringContent("A Person"), "engineerName" },
                { new StringContent("123456"), "engineerRegistration" }
            };
            var pdf = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n"));
            pdf.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            form.Add(pdf, "file", "x.pdf");
            var response = await client.PostAsync("/api/v1/certificates", form);
            response.StatusCode.Should().Be((HttpStatusCode)422);
        }

        [Fact]
        public async Task Schedule_of_rates_is_available()
        {
            var response = await _factory.ClientFor("AVONPH").GetAsync("/api/v1/schedule-of-rates");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JArray.Parse(await response.Content.ReadAsStringAsync()).Should().Contain(t => (string)t["code"] == "HT0301");
        }

        [Fact]
        public async Task Swagger_document_is_published()
        {
            var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("/api/v1/workorders/{reference}/complete");
        }
    }
}
