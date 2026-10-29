using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Hosting.Security;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fabrikam.Housing.Api.Tests.BackOffice
{
    public class BackOfficePageTests : IClassFixture<BackOfficeFactory>
    {
        private readonly BackOfficeFactory _factory;

        public BackOfficePageTests(BackOfficeFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Properties")]
        [InlineData("/Properties?q=BS16&patch=NORTH")]
        [InlineData("/Properties/Details/{property}")]
        [InlineData("/Tenancies")]
        [InlineData("/Tenancies?q=Smith")]
        [InlineData("/Tenancies/Details/{tenancy}")]
        [InlineData("/Tenancies/Statement/{tenancy}")]
        [InlineData("/Repairs")]
        [InlineData("/Repairs?overdue=true&priority=Emergency")]
        [InlineData("/Repairs/Triage")]
        [InlineData("/Repairs/Details/{order}")]
        [InlineData("/Repairs/Board")]
        [InlineData("/Repairs/BoardData?patch=EAST")]
        [InlineData("/Repairs/Raise?propertyId={property}")]
        [InlineData("/Compliance")]
        [InlineData("/Compliance?type=GasSafety&daysAhead=30")]
        [InlineData("/Compliance/Upload?propertyId={property}")]
        [InlineData("/Contractors")]
        [InlineData("/Contractors/Edit/1")]
        [InlineData("/Contractors/Invoices")]
        [InlineData("/Contractors/Invoices?status=Queried")]
        [InlineData("/Documents?q=certificate")]
        [InlineData("/Documents/Upload?propertyId={property}")]
        [InlineData("/Reports")]
        [InlineData("/Admin")]
        [InlineData("/Admin/BankHolidays")]
        [InlineData("/Admin/ScheduleOfRates")]
        [InlineData("/Admin/EditRate/1")]
        [InlineData("/Admin/RentReview")]
        public async Task Page_renders_for_an_administrator(string url)
        {
            int property, tenancy, order;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HousingDbContext>();
                tenancy = db.ArrearsCases.OrderBy(c => c.Id).Select(c => c.TenancyId).First();
                property = db.Tenancies.Single(t => t.Id == tenancy).PropertyId;
                order = db.WorkOrders.OrderByDescending(w => w.Id).Select(w => w.Id).First();
            }
            url = url.Replace("{property}", property.ToString()).Replace("{tenancy}", tenancy.ToString()).Replace("{order}", order.ToString());

            var client = _factory.ClientAs(@"CORP\it.admin", HousingRoles.Admin);
            var response = await client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
    }
}
