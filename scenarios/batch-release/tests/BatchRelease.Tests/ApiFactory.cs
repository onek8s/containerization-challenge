using System.Net;
using BatchRelease.Api;
using BatchRelease.Contracts;
using BatchRelease.Core.Model;
using BatchRelease.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BatchRelease.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string MesKey = "br_mes0tku1_7yQ2c9WvHn4KsLx0aPeR";
    public const string ErpKey = "br_erp00001_Zq81mBvN3sXcT6uYw2Lk";
    public const string OtherSiteKey = "br_mes0oul1_Hh3kP0qWz8vB5nM2xC7j";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeSigningHost Signing { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:BatchRelease", "Server=unused");
        builder.UseSetting("SigningHost:BaseAddress", "https://signing.invalid/");

        _connection.Open();
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BatchReleaseContext>>();
            services.AddDbContext<BatchReleaseContext>(o => o.UseSqlite(_connection));
            services.RemoveAll<ISigningHostClient>();
            services.AddSingleton<ISigningHostClient>(Signing);
        });
    }

    public void Seed()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BatchReleaseContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        db.ProductFamilies.Add(new ProductFamily { Code = "OSD", Name = "Oral solid dose", PolicySddl = "O:BAG:SYD:P(A;;0x1;;;AU)" });
        db.Products.Add(new Product { Code = "PRX-0450", Name = "Proxanol 450 mg film-coated tablets", FamilyCode = "OSD", ShelfLifeMonths = 36, MinimumRemainingMonths = 18 });
        db.Products.Add(new Product { Code = "PRX-0900", Name = "Proxanol 900 mg (withdrawn)", FamilyCode = "OSD", ShelfLifeMonths = 36, Active = false });
        db.ApiClients.Add(Client("MES Turku", MesKey, submit: true, read: false, site: "TKU"));
        db.ApiClients.Add(Client("ERP", ErpKey, submit: false, read: true, site: null));
        db.ApiClients.Add(Client("MES Oulu", OtherSiteKey, submit: true, read: true, site: "OUL"));
        db.SaveChanges();
    }

    private static ApiClient Client(string name, string key, bool submit, bool read, string? site) => new()
    {
        Name = name,
        KeyPrefix = key.Split('_')[1],
        KeyHash = ApiKeyAuthenticationHandler.Hash(key),
        CanSubmit = submit,
        CanRead = read,
        SiteCode = site,
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}

public sealed class FakeSigningHost : ISigningHostClient
{
    public List<(int BatchId, string Kind, string FileName, byte[] Content, string By)> Stored { get; } = new();
    public Dictionary<string, byte[]> Certificates { get; } = new();

    public Task<DocumentStored> StoreDocumentAsync(int batchId, string kind, string fileName, string contentType, Stream content, string submittedBy, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        Stored.Add((batchId, kind, fileName, ms.ToArray(), submittedBy));
        return Task.FromResult(new DocumentStored { DocumentId = Stored.Count, Kind = kind, FileName = fileName, Length = ms.Length, Sha256 = Core.Certificates.CertificateSerializer.Sha256Hex(ms.ToArray()) });
    }

    public Task<byte[]?> GetCertificateAsync(string certificateNumber, CancellationToken ct = default) =>
        Task.FromResult(Certificates.TryGetValue(certificateNumber, out var bytes) ? bytes : null);

    public Task<DecisionResult> DecideAsync(DecisionRequest request, CancellationToken ct = default) =>
        Task.FromResult(new DecisionResult(HttpStatusCode.NotImplemented, null, null));

    public Task<AccessResponse> GetAccessAsync(int batchId, string sid, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<DocumentContent?> OpenDocumentAsync(int batchId, int documentId, string sid, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<SigningHostStatus> GetStatusAsync(CancellationToken ct = default) => throw new NotSupportedException();
}
