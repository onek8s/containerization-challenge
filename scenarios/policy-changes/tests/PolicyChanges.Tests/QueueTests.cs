using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolicyChanges.Core.Flows;
using PolicyChanges.Core.Requests;
using PolicyChanges.Data;
using PolicyChanges.Data.Services;

namespace PolicyChanges.Tests;

public sealed class QueueTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero));

    public QueueTests()
    {
        _connection.Open();
        using var db = Db();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PolicyChangesContext Db() =>
        new(new DbContextOptionsBuilder<PolicyChangesContext>().UseSqlite(_connection).Options);

    private ChangeService Changes(PolicyChangesContext db) => new(db, new ChangeValidator(_time), _time);

    private RequestQueue Queue(PolicyChangesContext db) =>
        new(db, _time, Options.Create(new QueueOptions { LeaseMinutes = 10, MaxAttempts = 3, RetryDelayMinutes = new[] { 5, 15 } }));

    private async Task<string> SubmitAddressAsync(string policy = "HL-1042118", int daysAhead = 0)
    {
        using var db = Db();
        var result = await Changes(db).SubmitAsync(new ChangeDraft
        {
            PolicyNumber = policy,
            ChangeType = ChangeType.AddressChange,
            EffectiveDate = new DateOnly(2026, 10, 29).AddDays(daysAhead),
            Fields = new() { ["street"] = " Hämeenkatu 1 ", ["PostalCode"] = "33100", ["City"] = "Tampere", ["Country"] = "fi" },
        }, @"CORP\aino", ChangeChannel.Portal);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.Request!.Reference;
    }

    [Fact]
    public async Task Submit_assigns_daily_references_and_normalises_fields()
    {
        var first = await SubmitAddressAsync("HL-1042118");
        var second = await SubmitAddressAsync("HL-1042126");
        Assert.Equal("PC-261029-0001", first);
        Assert.Equal("PC-261029-0002", second);

        using var db = Db();
        var r = await Changes(db).FindAsync(first);
        Assert.Equal("Hämeenkatu 1", r!.Fields["Street"]);
        Assert.Equal("FI", r.Fields["Country"]);
        Assert.Equal(ChangeStatus.Queued, r.Status);
        Assert.Single(r.Events);
    }

    [Fact]
    public async Task Same_change_cannot_be_queued_twice()
    {
        await SubmitAddressAsync();
        using var db = Db();
        var again = await Changes(db).SubmitAsync(new ChangeDraft
        {
            PolicyNumber = "HL-1042118", ChangeType = ChangeType.AddressChange, EffectiveDate = new DateOnly(2026, 10, 30),
            Fields = new() { ["Street"] = "Other 2", ["PostalCode"] = "33100", ["City"] = "Tampere", ["Country"] = "FI" },
        }, "x", ChangeChannel.Portal);
        Assert.Contains(again.Errors, e => e.Contains("already waiting"));
    }

    [Fact]
    public async Task Claims_by_effective_date_and_completes()
    {
        var later = await SubmitAddressAsync("HL-1042118", daysAhead: 5);
        var sooner = await SubmitAddressAsync("HL-1042126", daysAhead: 1);

        using var db = Db();
        var queue = Queue(db);
        var claimed = await queue.ClaimNextAsync("DESK01");
        Assert.Equal(sooner, claimed!.Reference);
        Assert.Equal(ChangeStatus.InProgress, claimed.Status);
        Assert.Equal(1, claimed.Attempts);

        var other = await Queue(Db()).ClaimNextAsync("DESK02");
        Assert.Equal(later, other!.Reference);
        Assert.Null(await Queue(Db()).ClaimNextAsync("DESK03"));

        await queue.CompleteAsync(claimed.Id, "DESK01", FlowOutcome.Applied, "Endorsement 6004101 created.", "6004101",
            new[] { new EvidenceFile("endorsement", @"2026\10\x\01-01-endorsement.png") });

        using var check = Db();
        var done = await Changes(check).FindAsync(sooner);
        Assert.Equal(ChangeStatus.Applied, done!.Status);
        Assert.Equal("6004101", done.Endorsement);
        Assert.Null(done.LockedBy);
        Assert.Single(done.Evidence);
    }

    [Fact]
    public async Task Failures_are_retried_with_delay_then_go_to_review()
    {
        var reference = await SubmitAddressAsync();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var db = Db();
            var queue = Queue(db);
            var r = await queue.ClaimNextAsync("DESK01");
            Assert.NotNull(r);
            Assert.Equal(attempt, r!.Attempts);
            await queue.CompleteAsync(r.Id, "DESK01", FlowOutcome.Failed, "Window did not appear.", null, Array.Empty<EvidenceFile>());

            Assert.Null(await Queue(Db()).ClaimNextAsync("DESK01"));
            _time.Advance(TimeSpan.FromMinutes(15));
        }

        using var check = Db();
        var changes = Changes(check);
        Assert.Equal(ChangeStatus.NeedsReview, (await changes.FindAsync(reference))!.Status);

        Assert.True(await changes.RequeueAsync(reference, "lead"));
        var again = await Queue(Db()).ClaimNextAsync("DESK02");
        Assert.Equal(1, again!.Attempts);
    }

    [Fact]
    public async Task Expired_lease_is_taken_over_and_old_station_cannot_complete()
    {
        await SubmitAddressAsync();
        var r = await Queue(Db()).ClaimNextAsync("DESK01");
        _time.Advance(TimeSpan.FromMinutes(11));

        var taken = await Queue(Db()).ClaimNextAsync("DESK02");
        Assert.Equal(r!.Id, taken!.Id);
        Assert.Equal(2, taken.Attempts);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Queue(Db()).CompleteAsync(r.Id, "DESK01", FlowOutcome.Applied, "late", "6004101", Array.Empty<EvidenceFile>()));
    }

    [Fact]
    public async Task Release_does_not_count_the_attempt()
    {
        var reference = await SubmitAddressAsync();
        var r = await Queue(Db()).ClaimNextAsync("DESK01");
        await Queue(Db()).ReleaseAsync(r!.Id, "DESK01", "desktop became unavailable");

        using var db = Db();
        var after = await Changes(db).FindAsync(reference);
        Assert.Equal(ChangeStatus.Queued, after!.Status);
        Assert.Equal(0, after.Attempts);
    }

    [Fact]
    public async Task Heartbeat_tracks_station()
    {
        using var db = Db();
        var queue = Queue(db);
        await queue.HeartbeatAsync("DESK01", @"CORP\svc-desk01", "Idle", "Console session", "6.4.0.112", null);
        await queue.HeartbeatAsync("DESK01", @"CORP\svc-desk01", "Idle", null, null, null, FlowOutcome.Applied);

        var station = Assert.Single(await Changes(Db()).StationsAsync());
        Assert.Equal("6.4.0.112", station.ClientVersion);
        Assert.Equal(1, station.AppliedCount);
    }
}
