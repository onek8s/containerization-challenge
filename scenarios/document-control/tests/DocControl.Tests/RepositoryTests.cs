using Litware.DocControl.Data;
using Litware.DocControl.Documents;
using Litware.DocControl.Transmittals;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Tests;

public sealed class RepositoryTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"dc-test-{Guid.NewGuid():N}.db");
    private readonly DbSession _db;

    public RepositoryTests()
    {
        _db = new DbSession(Options.Create(new DatabaseOptions
        {
            Provider = "Sqlite",
            ConnectionString = $"Data Source={_file};Pooling=False",
            CreateSampleData = true,
        }));
        DbSession.ResetInitialisation();
    }

    public void Dispose()
    {
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public async Task Register_shows_latest_and_approved_revision()
    {
        var docs = new DocumentRepository(_db);
        var register = await docs.RegisterAsync("P1042");
        Assert.Equal(3, register.Count);

        var ga = register.Single(d => d.Number == "P1042-LIT-ME-DRG-0012");
        Assert.Equal("P02", ga.LatestRevision);
        Assert.Equal("P02", ga.ApprovedRevision);
        Assert.Equal(RevisionStatus.Approved, ga.LatestStatus);

        Assert.Single(await docs.RegisterAsync("P1042", search: "motor"));
    }

    [Fact]
    public async Task Approving_supersedes_previous_revision()
    {
        var docs = new DocumentRepository(_db);
        var revs = new RevisionRepository(_db);
        var doc = (await docs.FindAsync("P1042-LIT-ME-DRG-0012"))!;

        var id = await revs.AddAsync(new RevisionRecord
        {
            DocumentId = doc.Id, Code = "P03", Status = RevisionStatus.Submitted, FileName = "x.pdf", VaultPath = "x",
            Sha256 = "AB", Source = "Portal", SubmittedBy = "CORP\\test", SubmittedUtc = DateTime.UtcNow,
        });

        Assert.Contains(await revs.PendingReviewAsync("P1042"), r => r.Id == id);
        Assert.True(await revs.ApproveAsync(id, "CORP\\lniemi", null));
        Assert.False(await revs.ApproveAsync(id, "CORP\\lniemi", null));

        var history = await revs.ForDocumentAsync(doc.Id);
        Assert.Equal(RevisionStatus.Approved, history.Single(r => r.Code == "P03").Status);
        Assert.Equal(RevisionStatus.Superseded, history.Single(r => r.Code == "P02").Status);
        Assert.True(await revs.HashExistsAsync(doc.Id, "AB"));
    }

    [Fact]
    public async Task Transmittals_get_numbers_and_are_claimed_once()
    {
        var revs = new RevisionRepository(_db);
        var trs = new TransmittalRepository(_db);
        var issuable = await revs.IssuableAsync("P1042", includeSubmitted: false);
        Assert.Equal(2, issuable.Count);

        TransmittalRecord New() => new()
        {
            ProjectCode = "P1042", Purpose = TransmittalPurpose.ForInformation, RecipientCompany = "Pohjola Pulp Oy",
            RecipientEmail = "docs@pohjola.example", CreatedBy = "CORP\\lniemi", CreatedByEmail = "liisa.niemi@litware.example",
        };

        var first = await trs.CreateAsync(New(), issuable.Select(r => r.Id).ToList());
        var second = await trs.CreateAsync(New(), [issuable[0].Id]);
        Assert.Equal("TR-P1042-00001", first.Number);
        Assert.Equal("TR-P1042-00002", second.Number);
        Assert.Equal(2, (await trs.ItemsAsync(first.Id)).Count);

        var claimed = await trs.ClaimNextAsync("pkg-1", DateTime.UtcNow.AddHours(-1), default);
        Assert.Equal(first.Id, claimed!.Id);
        Assert.Equal(TransmittalStatus.Building, claimed.Status);
        Assert.Equal(1, claimed.Attempts);

        var next = await trs.ClaimNextAsync("pkg-2", DateTime.UtcNow.AddHours(-1), default);
        Assert.Equal(second.Id, next!.Id);
        Assert.Null(await trs.ClaimNextAsync("pkg-3", DateTime.UtcNow.AddHours(-1), default));

        // abandoned builds are picked up again after the stale time
        var stale = await trs.ClaimNextAsync("pkg-3", DateTime.UtcNow.AddMinutes(1), default);
        Assert.NotNull(stale);

        await trs.MarkIssuedAsync(first.Id, "2024/TR-P1042-00001.zip", 1234);
        var issued = (await trs.FindAsync("TR-P1042-00001"))!;
        Assert.Equal(TransmittalStatus.Issued, issued.Status);
        Assert.Equal(1234, issued.PackageSize);
    }
}
