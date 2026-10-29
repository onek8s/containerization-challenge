using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Hosting.Security;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fabrikam.Housing.Api.Tests.BackOffice
{
    public class BackOfficeTests : IClassFixture<BackOfficeFactory>
    {
        private readonly BackOfficeFactory _factory;

        public BackOfficeTests(BackOfficeFactory factory)
        {
            _factory = factory;
        }

        private T Query<T>(System.Func<HousingDbContext, T> query)
        {
            using (var scope = _factory.Services.CreateScope())
                return query(scope.ServiceProvider.GetRequiredService<HousingDbContext>());
        }

        [Fact]
        public async Task Anonymous_requests_are_challenged()
        {
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("/");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Dashboard_shows_counts_and_arrears()
        {
            var client = _factory.ClientAs(@"CORP\j.okafor", HousingRoles.Officer);
            var html = await client.GetStringAsync("/?patch=NORTH");
            html.Should().Contain("Open repairs").And.Contain("Highest arrears in NORTH").And.Contain("Test CORP\\j.okafor");
        }

        [Fact]
        public async Task Property_search_by_postcode_district_finds_the_estate()
        {
            var property = Query(db => db.Properties.Include(p => p.Estate).OrderBy(p => p.Id).First(p => p.Estate.Code == "MILL"));
            var client = _factory.ClientAs(@"CORP\j.okafor", HousingRoles.Officer);

            var html = await client.GetStringAsync("/Properties?q=" + WebUtility.UrlEncode(property.Postcode));
            html.Should().Contain(property.Reference);

            var details = await client.GetStringAsync("/Properties/Details/" + property.Id);
            details.Should().Contain(property.AddressLine1).And.Contain("GasSafety").And.Contain("Electrical");
        }

        [Fact]
        public async Task Repairs_desk_raises_and_assigns_a_work_order()
        {
            var propertyId = Query(db => db.Properties.OrderBy(p => p.Id).Skip(3).First().Id);
            var client = _factory.ClientAs(@"CORP\repairs.desk", HousingRoles.Repairs);
            var token = await BackOfficeFactory.AntiforgeryTokenAsync(client, "/Repairs/Raise?propertyId=" + propertyId);

            var raise = await client.PostAsync("/Repairs/Raise", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["PropertyId"] = propertyId.ToString(),
                ["Location"] = "Kitchen",
                ["Description"] = "Mixer tap leaking at the base",
                ["SorCode"] = "PL0105",
                ["Quantity"] = "1",
                ["ReportedBy"] = "Phone call"
            }));
            raise.StatusCode.Should().Be(HttpStatusCode.Redirect, await raise.Content.ReadAsStringAsync());
            var location = raise.Headers.Location.ToString();
            var id = int.Parse(location.Substring(location.LastIndexOf('/') + 1));

            var order = Query(db => db.WorkOrders.Include(w => w.Lines).Single(w => w.Id == id));
            order.Status.Should().Be(WorkOrderStatus.Reported);
            order.Channel.Should().Be(ReportChannel.Staff);
            order.Priority.Should().Be(RepairPriority.Routine);
            order.Lines.Single().UnitPrice.Should().Be(96.20m);

            var detailsToken = await BackOfficeFactory.AntiforgeryTokenAsync(client, "/Repairs/Details/" + id);
            var contractorId = Query(db => db.Contractors.Single(c => c.Code == "AVONPH").Id);
            var assign = await client.PostAsync($"/Repairs/Assign/{id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = detailsToken,
                ["contractorId"] = contractorId.ToString()
            }));
            assign.StatusCode.Should().Be(HttpStatusCode.Redirect);

            order = Query(db => db.WorkOrders.Include(w => w.Events).Single(w => w.Id == id));
            order.Status.Should().Be(WorkOrderStatus.Assigned);
            order.ContractorId.Should().Be(contractorId);
            order.Events.Select(e => e.NewStatus).Should().Contain(new WorkOrderStatus?[] { WorkOrderStatus.Triaged, WorkOrderStatus.Assigned });
        }

        [Fact]
        public async Task Posting_an_adjustment_needs_the_income_role()
        {
            var tenancyId = Query(db => db.Tenancies.OrderBy(t => t.Id).Skip(7).First().Id);
            var form = new Dictionary<string, string>
            {
                ["Adjustment.Type"] = TransactionType.Adjustment.ToString(),
                ["Adjustment.Amount"] = "-12.50",
                ["Adjustment.Description"] = "Goodwill for missed appointment"
            };

            var compliance = _factory.ClientAs(@"CORP\c.ward", HousingRoles.Compliance);
            form["__RequestVerificationToken"] = await BackOfficeFactory.AntiforgeryTokenAsync(compliance, "/Tenancies/Details/" + tenancyId);
            (await compliance.PostAsync($"/Tenancies/PostAdjustment/{tenancyId}", new FormUrlEncodedContent(form)))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var income = _factory.ClientAs(@"CORP\m.hughes", HousingRoles.Income);
            form["__RequestVerificationToken"] = await BackOfficeFactory.AntiforgeryTokenAsync(income, "/Tenancies/Details/" + tenancyId);
            (await income.PostAsync($"/Tenancies/PostAdjustment/{tenancyId}", new FormUrlEncodedContent(form)))
                .StatusCode.Should().Be(HttpStatusCode.Redirect);

            var tx = Query(db => db.RentTransactions.Where(r => r.TenancyId == tenancyId && r.Type == TransactionType.Adjustment).OrderByDescending(r => r.Id).First());
            tx.Amount.Should().Be(-12.50m);
            tx.Source.Should().Be(@"BackOffice:CORP\m.hughes");
            Query(db => db.Set<Common.AuditEntry>().Count(a => a.Actor == @"CORP\m.hughes" && a.Action == "PostedAdjustment")).Should().Be(1);
        }

        [Fact]
        public async Task Arrears_report_downloads_as_xlsx()
        {
            var client = _factory.ClientAs(@"CORP\m.hughes", HousingRoles.Income);
            var response = await client.GetAsync("/Reports/Arrears?minimum=10");
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            response.Content.Headers.ContentType.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            bytes.Take(2).Should().Equal((byte)'P', (byte)'K');
        }

        [Fact]
        public async Task Statement_pdf_is_generated()
        {
            var tenancyId = Query(db => db.Tenancies.OrderBy(t => t.Id).First().Id);
            var client = _factory.ClientAs(@"CORP\m.hughes", HousingRoles.Income);
            var response = await client.GetAsync($"/Tenancies/StatementPdf/{tenancyId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            response.Content.Headers.ContentType.MediaType.Should().Be("application/pdf");
        }

        [Fact]
        public async Task Admin_pages_need_the_admin_role()
        {
            (await _factory.ClientAs(@"CORP\j.okafor", HousingRoles.Officer).GetAsync("/Admin/RentReview")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var html = await _factory.ClientAs(@"CORP\it.admin", HousingRoles.Admin).GetStringAsync("/Admin/RentReview?cpi=3.1");
            html.Should().Contain("Annual rent review");
        }
    }
}
