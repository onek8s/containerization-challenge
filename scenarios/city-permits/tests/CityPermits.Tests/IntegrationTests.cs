using CityPermits.Contracts;
using CityPermits.Data;
using CityPermits.Data.Seed;
using CityPermits.Documents;
using CityPermits.Domain.Model;
using CityPermits.Gis;
using CityPermits.Gis.Development;
using CityPermits.Integration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CityPermits.Tests;

public class IntegrationTests
{
    private static readonly string Samples = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Samples;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static DevelopmentGisService Gis() => new(Options.Create(new GisOptions { DevelopmentDataPath = "gis" }), new Env());

    [Fact]
    public void Development_gis_finds_constraints_for_town_centre_parcel()
    {
        var s = Gis().CheckSite(new SiteCheckRequest("ASH-00140", "POLYGON((601365 142295,601380 142295,601380 142310,601365 142310,601365 142295))"));
        Assert.Equal("TC", s.ZoningCode);
        Assert.Equal("3a", s.FloodZone);
        Assert.Equal("A", s.ParkingZone);
        Assert.True(s.DistanceToSchoolM < 400);
    }

    [Fact]
    public void Development_gis_renders_png()
    {
        var img = Gis().ExportMap(new MapExportRequest("SiteLocation", "ASH-00001", null, "t", null));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, img.Content.Take(4));
    }

    [Fact]
    public async Task Seed_creates_cases_in_all_kinds_on_sqlite()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var db = new PermitsDbContext(new DbContextOptionsBuilder<PermitsDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        var gis = Gis();
        await SampleData.SeedAsync(db, SampleData.ReadAddresses(Path.Combine(Samples, "address-register.csv")), 45, DateTime.UtcNow,
            c => c.ParcelId is null ? null : gis.CheckSite(new SiteCheckRequest(c.ParcelId, null)));
        Assert.Equal(45, await db.Cases.CountAsync());
        Assert.Equal(3, await db.Cases.Select(c => c.Kind).Distinct().CountAsync());
        Assert.True(await db.Cases.AnyAsync(c => c.Status == CaseStatus.Issued));
        Assert.Equal(await db.Cases.CountAsync(), await db.Cases.Select(c => c.Reference).Distinct().CountAsync());
    }

    [Fact]
    public void Documents_render_pdf()
    {
        var c = new PermitCase
        {
            Reference = "BL/2024/00001B", Kind = PermitKind.BusinessLicence, Title = "Food premises licence", Outcome = DecisionOutcome.Approved,
            DecidedUtc = DateTime.UtcNow, ValidFrom = new DateOnly(2024, 1, 1), ValidTo = new DateOnly(2024, 12, 31),
            Applicant = new Applicant { DisplayName = "Elwick Bakery Ltd" }, Business = new BusinessDetails { Category = BusinessCategory.FoodPremises, TradingName = "Elwick Bakery" }
        };
        var docs = new PermitDocuments();
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(docs.PermitCertificate(c).Content, 0, 4));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(docs.DecisionLetter(c, null).Content, 0, 4));
    }

    [Fact]
    public void Paystream_signature_round_trip()
    {
        var client = new PaystreamClient(new PaystreamOptions { WebhookSecret = "whsec_test" });
        var now = DateTimeOffset.UtcNow;
        var ts = now.ToUnixTimeSeconds().ToString();
        var body = "{\"eventId\":\"e1\"}";
        Assert.True(client.VerifySignature($"t={ts},v1={PaystreamClient.Sign("whsec_test", ts, body)}", body, now));
        Assert.False(client.VerifySignature($"t={ts},v1={PaystreamClient.Sign("other", ts, body)}", body, now));
        Assert.False(client.VerifySignature($"t={ts},v1={PaystreamClient.Sign("whsec_test", ts, body)}", body, now.AddMinutes(10)));
    }

    [Fact]
    public void Address_file_search()
    {
        var reg = new AddressRegisterFile(Path.Combine(Samples, "address-register.csv"));
        var hits = reg.Search("Station Road");
        Assert.NotEmpty(hits);
        Assert.Equal(hits[0].Uprn, reg.Get(hits[0].Uprn)!.Uprn);
    }
}
