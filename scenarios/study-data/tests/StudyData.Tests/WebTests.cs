using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StudyData.Tests;

public class WebTests
{
    private static WebApplicationFactory<T> Factory<T>(string db) where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:StudyData", $"Data Source={db}");
            b.UseSetting("Edc:SeedSampleData", "true");
            b.UseSetting("Edc:SourceDocumentVault", Path.Combine(Path.GetDirectoryName(db)!, "vault"));
            b.UseSetting("Logging:LogFile", Path.Combine(Path.GetDirectoryName(db)!, "logs", "test-.log"));
        });

    [Fact]
    public async Task SitePortalSignsInAndShowsSites()
    {
        var db = Path.Combine(Directory.CreateTempSubdirectory().FullName, "edc.db");
        using var factory = Factory<StudyData.Edc.Web.Services.EdcOptions>(db);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var redirect = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);

        var login = await client.GetStringAsync("/account/login");
        var token = System.Text.RegularExpressions.Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var post = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "aino.virtanen@hus.example",
            ["Password"] = StudyData.Data.Seed.DbInitializer.DevelopmentPassword,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
    }

    [Fact]
    public async Task SponsorApiOnlyShowsTheClientsStudies()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var db = Path.Combine(dir, "sponsor.db");
        // Seed through the site portal first.
        using (var edc = Factory<StudyData.Edc.Web.Services.EdcOptions>(db))
        {
            await edc.CreateClient().GetAsync("/health");
        }

        using var factory = Factory<StudyData.Sponsor.Api.SponsorAccess>(db);
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/studies/FAB-ONC-201")).StatusCode);

        client.DefaultRequestHeaders.Add("X-Client-Id", "tailspin-cdms");
        var study = await client.GetFromJsonAsync<Dictionary<string, object>>("/api/v1/studies/FAB-ONC-201");
        Assert.Equal("FAB-ONC-201", study!["code"].ToString());
        var subjects = await client.GetStringAsync("/api/v1/studies/FAB-ONC-201/subjects");
        Assert.Contains("101-001", subjects);
        Assert.Contains("\"arm\":null", subjects);

        var export = await client.PostAsJsonAsync("/api/v1/studies/FAB-ONC-201/exports", new { includeArm = true });
        Assert.Equal(HttpStatusCode.Forbidden, export.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/studies/FAB-ONC-201/exports", new { includeArm = false })).StatusCode);

        var other = factory.CreateClient();
        other.DefaultRequestHeaders.Add("X-Client-Id", "unknown-client");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/v1/studies/FAB-ONC-201")).StatusCode);
    }
}
