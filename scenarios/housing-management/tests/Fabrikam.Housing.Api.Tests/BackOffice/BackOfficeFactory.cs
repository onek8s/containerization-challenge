using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fabrikam.Housing.Integration.DocumentStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Api.Tests.BackOffice
{
    /// <summary>
    /// Runs the back office against a seeded SQLite database in a temp folder, with simulated integrations,
    /// an in-memory document service and a header-driven test sign-in instead of the identity server.
    /// </summary>
    public class BackOfficeFactory : WebApplicationFactory<Fabrikam.Housing.BackOffice.Startup>
    {
        public BackOfficeFactory()
        {
            Folder = Path.Combine(Path.GetTempPath(), "housing-backoffice-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
        }

        public string Folder { get; }
        public FakeDocumentServiceClient Documents { get; } = new FakeDocumentServiceClient();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(FindProjectFolder());
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:Housing"] = "Data Source=" + Path.Combine(Folder, "housing.db"),
                    ["Database:Provider"] = "Sqlite",
                    ["Integration:Mode"] = "Simulated",
                    ["Integration:SimulatorFolder"] = Path.Combine(Folder, "simulators"),
                    ["DataProtection:KeyFolder"] = Path.Combine(Folder, "keys"),
                    ["DataProtection:ProtectWithDpapi"] = "false",
                    ["Documents:PdfEngine"] = "Basic",
                    ["Logging:Folder"] = Path.Combine(Folder, "logs")
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(o =>
                {
                    o.DefaultScheme = TestAuthHandler.Scheme;
                    o.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                    o.DefaultChallengeScheme = TestAuthHandler.Scheme;
                    o.DefaultForbidScheme = TestAuthHandler.Scheme;
                    o.DefaultSignInScheme = TestAuthHandler.Scheme;
                });
                services.RemoveAll<IDocumentServiceClient>();
                services.AddSingleton<IDocumentServiceClient>(Documents);
            });
        }

        public HttpClient ClientAs(string login, params string[] roles)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, login);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
            return client;
        }

        public static async Task<string> AntiforgeryTokenAsync(HttpClient client, string url)
        {
            var html = await client.GetStringAsync(url);
            var match = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
            if (!match.Success) throw new InvalidOperationException("No anti-forgery token on " + url);
            return match.Groups[1].Value;
        }

        private static string FindProjectFolder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HousingManagement.sln"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("HousingManagement.sln not found above " + AppContext.BaseDirectory);
            return Path.Combine(dir.FullName, "src", "Fabrikam.Housing.BackOffice");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { Directory.Delete(Folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public new const string Scheme = "Test";
        public const string UserHeader = "X-Test-User";
        public const string RolesHeader = "X-Test-Roles";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim>
            {
                new Claim("sub", user.ToString()),
                new Claim("preferred_username", user.ToString()),
                new Claim("name", "Test " + user)
            };
            if (Request.Headers.TryGetValue(RolesHeader, out var roles))
                claims.AddRange(roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => new Claim("role", r.Trim())));
            var identity = new ClaimsIdentity(claims, Scheme, "name", "role");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
