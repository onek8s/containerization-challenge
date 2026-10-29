using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Integration.DocumentStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Api.Tests.Support
{
    /// <summary>
    /// Contractor API on a seeded SQLite database with simulated integrations. Requests carry the contractor
    /// in the X-Test-Contractor header instead of a token from the identity server.
    /// </summary>
    public class ContractorApiFactory : WebApplicationFactory<Fabrikam.Housing.ContractorApi.Startup>
    {
        public static readonly DateTime Now = new DateTime(2021, 6, 14, 10, 0, 0);
        public const string WebhookSecret = "test-webhook-secret";

        public ContractorApiFactory()
        {
            Folder = new TestFolder();
        }

        public TestFolder Folder { get; }
        public FakeDocumentServiceClient Documents { get; } = new FakeDocumentServiceClient();
        public FixedClock Clock { get; } = new FixedClock(Now);
        public int RequestsPerMinute { get; set; } = 10000;

        public string DatabasePath => Folder.Sub("housing.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:Housing"] = "Data Source=" + DatabasePath,
                    ["Database:Provider"] = "Sqlite",
                    ["Integration:Mode"] = "Simulated",
                    ["Integration:SimulatorFolder"] = Folder.Sub("simulators"),
                    ["Integration:Payments:WebhookSecret"] = WebhookSecret,
                    ["DataProtection:KeyFolder"] = Folder.Sub("keys"),
                    ["DataProtection:ProtectWithDpapi"] = "false",
                    ["Logging:Folder"] = Folder.Sub("logs"),
                    ["ApiLimits:RequestsPerMinute"] = RequestsPerMinute.ToString()
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IClock>(Clock);
                services.AddSingleton<IDocumentServiceClient>(Documents);
                services.AddAuthentication(TestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(o =>
                {
                    o.DefaultScheme = TestAuthHandler.Scheme;
                    o.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                    o.DefaultChallengeScheme = TestAuthHandler.Scheme;
                });
            });
        }

        public HttpClient ClientFor(string contractorCode)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.Header, contractorCode);
            return client;
        }

        public HousingDbContext CreateDbContext()
        {
            return new HousingDbContext(new DbContextOptionsBuilder<HousingDbContext>().UseSqlite("Data Source=" + DatabasePath).Options);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) Folder.Dispose();
        }
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public new const string Scheme = "Test";
        public const string Header = "X-Test-Contractor";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var code)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("client_id", "contractor-" + code.ToString().ToLowerInvariant()),
                new Claim("scope", "contractor-api"),
                new Claim("client_contractor_code", code.ToString())
            }, Scheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
