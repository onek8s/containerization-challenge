using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Lucerne.Lending.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Lucerne.Lending.Tests;

public sealed class LendingApiFactory : WebApplicationFactory<Program>
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "lending-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Folder);
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Lending", "Data Source=" + Path.Combine(Folder, "lending.db"));
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:Upgrade", "true");
        builder.UseSetting("Core:Provider", "Simulator");
        builder.UseSetting("Core:SimulatorDatabase", Path.Combine(Folder, "core.db"));
        builder.UseSetting("Lending:RunBackgroundWorkers", "false");
        builder.UseSetting("ESign:CallbackSecret", "test-secret");
        builder.UseSetting("Seed:ApiClients:customer-portal:Key", "test-portal-key");
        builder.UseSetting("Seed:ApiClients:customer-portal:Scopes", "portal");
        builder.UseSetting("Seed:ApiClients:back-office:Key", "test-office-key");
        builder.UseSetting("Seed:ApiClients:back-office:Scopes", "office");
        builder.UseSetting("NLog:variables:logDir", Path.Combine(Folder, "logs"));
    }
}

public sealed class LendingApiTests : IClassFixture<LendingApiFactory>
{
    private readonly LendingApiFactory _factory;

    public LendingApiTests(LendingApiFactory factory) => _factory = factory;

    private HttpClient Client(string? key = "test-portal-key", string? onBehalfOf = null)
    {
        var client = _factory.CreateClient();
        if (key != null) client.DefaultRequestHeaders.Add("X-Api-Key", key);
        if (onBehalfOf != null) client.DefaultRequestHeaders.Add("X-On-Behalf-Of", onBehalfOf);
        return client;
    }

    [Fact]
    public async Task Requires_an_api_key()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).GetAsync("api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("wrong").GetAsync("api/products")).StatusCode);
    }

    [Fact]
    public async Task Lists_active_products()
    {
        var products = await Client().GetFromJsonAsync<List<ProductDto>>("api/products");
        Assert.Contains(products!, p => p.Code == "PL");
        Assert.Contains(products!, p => p.Code == "CAR" && p.RequiresCollateral);
        Assert.DoesNotContain(products!, p => p.Code == "FLEX");
    }

    [Fact]
    public async Task Calculator_returns_apr_and_example()
    {
        var response = await Client().PostAsJsonAsync("api/products/calculate", new CalculatorRequest { ProductCode = "PL", Amount = 10000, TermMonths = 60 });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CalculatorResponse>();
        Assert.True(result!.AprPercent > result.NominalRatePercent);
        Assert.Contains("10", result.Example);
    }

    [Fact]
    public async Task Calculator_rejects_amount_outside_product_limits()
    {
        var response = await Client().PostAsJsonAsync("api/products/calculate", new CalculatorRequest { ProductCode = "PL", Amount = 999999, TermMonths = 60 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submitted_application_is_visible_to_its_applicant_only()
    {
        var request = Application("150385-2481");
        var submit = await Client(onBehalfOf: "150385-2481").PostAsJsonAsync("api/applications", request);
        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);
        var created = await submit.Content.ReadFromJsonAsync<ApplicationCreated>();
        Assert.StartsWith("LA-", created!.Reference);
        Assert.Equal("Submitted", created.Status);

        var own = await Client().GetAsync($"api/applications/{created.Reference}?pic=150385-2481");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var detail = await own.Content.ReadFromJsonAsync<ApplicationDetailDto>();
        Assert.Equal("Aino Virtanen", detail!.ApplicantName);
        Assert.True(detail.CanCancel);

        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync($"api/applications/{created.Reference}?pic=020790-317C")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync($"api/applications/{created.Reference}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client("test-office-key").GetAsync($"api/applications/{created.Reference}")).StatusCode);

        var list = await Client().GetFromJsonAsync<List<ApplicationSummaryDto>>("api/customers/150385-2481/applications");
        Assert.Contains(list!, a => a.Reference == created.Reference);

        Assert.Equal(HttpStatusCode.NoContent, (await Client().PostAsync($"api/applications/{created.Reference}/cancel?pic=150385-2481", null)).StatusCode);
        var cancelled = await Client().GetFromJsonAsync<ApplicationDetailDto>($"api/applications/{created.Reference}?pic=150385-2481");
        Assert.Equal("Cancelled", cancelled!.Status);
    }

    [Fact]
    public async Task Invalid_application_is_rejected_with_reasons()
    {
        var request = Application("150385-2482");
        request.ConsentBureau = false;
        var response = await Client().PostAsJsonAsync("api/applications", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("identity code", body);
        Assert.Contains("Consent", body);
    }

    [Fact]
    public async Task Customer_loans_come_from_the_core()
    {
        var loans = await Client().GetFromJsonAsync<List<CustomerLoanDto>>("api/customers/140772-159C/loans");
        Assert.Contains(loans!, l => l.DaysPastDue >= 90 && l.Arrears > 0);
        Assert.All(loans!, l => Assert.True(Lucerne.Lending.Domain.PaymentReference.IsValidNational(l.PaymentReference)));
    }

    [Fact]
    public async Task Esign_callback_requires_valid_signature()
    {
        var body = "{\"orderId\":\"SH-1\",\"externalReference\":\"LA-2026-000001\",\"state\":\"Completed\",\"timestamp\":\"2026-05-01T10:00:00Z\"}";
        var client = _factory.CreateClient();
        var bad = new HttpRequestMessage(HttpMethod.Post, "api/esign/callback") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        bad.Headers.Add("X-SignHub-Signature", "00");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(bad)).StatusCode);

        var good = new HttpRequestMessage(HttpMethod.Post, "api/esign/callback") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        good.Headers.Add("X-SignHub-Signature", Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-secret"), Encoding.UTF8.GetBytes(body))).ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(good)).StatusCode);
    }

    [Fact]
    public async Task Health_reports_database_and_core()
    {
        var response = await _factory.CreateClient().GetAsync("health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static ApplicationRequest Application(string pic) => new()
    {
        ProductCode = "PL", Purpose = "Travel", Amount = 8000, TermMonths = 48, DueDay = 15, ConsentBureau = true,
        DisbursementIban = "FI2112345600000785",
        Applicant = new ApplicantDto
        {
            PersonalIdentityCode = pic, FirstName = "Aino", LastName = "Virtanen", Email = "aino.virtanen@example.fi", Phone = "+358401234501",
            StreetAddress = "Mannerheimintie 12 B 7", PostalCode = "00100", City = "Helsinki", Employment = "Permanent", Employer = "Kesko",
            EmployedSince = new DateTime(2015, 3, 1), GrossMonthlyIncome = 4300, NetMonthlyIncome = 3150, HousingCostMonthly = 850, Housing = "Rent"
        }
    };
}
