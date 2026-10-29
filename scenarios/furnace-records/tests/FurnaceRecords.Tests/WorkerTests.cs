using System.Net;
using FurnaceRecords.Core;
using FurnaceRecords.Data;
using FurnaceRecords.Data.Entities;
using FurnaceRecords.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace FurnaceRecords.Tests;

public sealed class WorkerFixture : IDisposable
{
    public SqliteDb Db { get; } = new();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 3, 2, 4, 0, 0, TimeSpan.Zero));
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "fr-worker-" + Guid.NewGuid().ToString("N"));
    public ServiceProvider Services { get; }

    public WorkerFixture()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Db.Create());
        Services = services.BuildServiceProvider();
    }

    public WorkerOptions Options(string? mes = null) => new()
    {
        MesAddress = mes ?? "http://mes.test/",
        CertificateFolder = Path.Combine(Folder, "Certificates"),
        MailPickupFolder = Path.Combine(Folder, "Mail"),
        TimeZone = "Europe/Berlin",
    };

    public async Task<Load> ReleasedLoadAsync()
    {
        using var db = Db.Create();
        var loads = new LoadService(db, Time);
        var load = await loads.PlanAsync(new PlanLoadRequest("F01", "ST-7075", new[] { new PlannedPart("WO-1", "P-1", "L1", 3) }, "MES01"));
        await loads.StartAsync(load, Time.GetUtcNow().UtcDateTime);
        await loads.CompleteAsync(load.Id, Time.GetUtcNow().UtcDateTime.AddHours(2), TimeSpan.FromSeconds(10));
        var stored = await db.Loads.AsNoTracking().SingleAsync(l => l.Id == load.Id);
        await new LoadService(Db.Create(), Time).DecideAsync(load.LoadNumber, true, "CORP\\q1", "MRB 26-1", stored.RowVersion);
        return stored;
    }

    public void Dispose()
    {
        Services.Dispose();
        Db.Dispose();
        if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
    }
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    public List<(string Path, string Body)> Requests { get; } = new();
    public HttpStatusCode Status { get; set; } = HttpStatusCode.Accepted;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken)));
        return new HttpResponseMessage(Status);
    }
}

internal sealed class HandlerFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    private readonly string _base;

    public HandlerFactory(HttpMessageHandler handler, string baseAddress)
    {
        _handler = handler;
        _base = baseAddress;
    }

    public HttpClient CreateClient(string name) => new(_handler, false) { BaseAddress = new Uri(_base) };
}

public class WorkerTests : IDisposable
{
    private readonly WorkerFixture _f = new();

    public void Dispose() => _f.Dispose();

    [Fact]
    public async Task Outbox_is_delivered_in_order_and_retried()
    {
        await _f.ReleasedLoadAsync();
        var handler = new RecordingHandler { Status = HttpStatusCode.ServiceUnavailable };
        var dispatcher = new OutboxDispatcher(_f.Services.GetRequiredService<IServiceScopeFactory>(), new HandlerFactory(handler, "http://mes.test/api/"),
            _f.Options(), _f.Time, NullLogger<OutboxDispatcher>.Instance);

        Assert.Equal(0, await dispatcher.DispatchAsync(CancellationToken.None));
        Assert.Single(handler.Requests);

        handler.Status = HttpStatusCode.Accepted;
        Assert.Equal(0, await dispatcher.DispatchAsync(CancellationToken.None));

        _f.Time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(2, await dispatcher.DispatchAsync(CancellationToken.None));
        Assert.Equal(new[] { "/api/events/load.completed", "/api/events/load.completed", "/api/events/load.released" }, handler.Requests.Select(r => r.Path));
        Assert.Contains("\"Status\":\"Released\"", handler.Requests[^1].Body);
        Assert.Contains("WO-1", handler.Requests[^1].Body);

        using var db = _f.Db.Create();
        Assert.All(await db.Outbox.ToListAsync(), m => Assert.NotNull(m.SentAtUtc));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 4)]
    [InlineData(6, 32)]
    [InlineData(20, 32)]
    public void Backoff_grows_and_is_capped(int attempts, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), OutboxDispatcher.Backoff(attempts));

    [Fact]
    public async Task Released_loads_are_archived_as_pdf()
    {
        var load = await _f.ReleasedLoadAsync();
        var archiver = new CertificateArchiver(_f.Services.GetRequiredService<IServiceScopeFactory>(), _f.Options(), _f.Time, NullLogger<CertificateArchiver>.Instance);

        Assert.Equal(1, await archiver.ArchiveAsync(CancellationToken.None));
        Assert.Equal(0, await archiver.ArchiveAsync(CancellationToken.None));

        using var db = _f.Db.Create();
        var path = (await db.Loads.SingleAsync(l => l.Id == load.Id)).CertificatePath!;
        Assert.EndsWith(Path.Combine("2026", $"HT-{load.LoadNumber}.pdf"), path);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 4));
    }

    [Fact]
    public async Task Reminder_runs_once_a_day_after_the_reminder_time()
    {
        using (var db = _f.Db.Create())
        {
            var ch = await db.Channels.Include(c => c.Furnace).FirstAsync(c => c.Furnace!.Code == "F01" && c.Channel == 4);
            ch.CalibrationDue = new DateOnly(2026, 3, 1);
            await db.SaveChangesAsync();
        }
        var reminder = new CalibrationReminder(_f.Services.GetRequiredService<IServiceScopeFactory>(), _f.Options(), _f.Time, NullLogger<CalibrationReminder>.Instance);

        // 06:00 Berlin is 05:00 UTC in winter.
        _f.Time.SetUtcNow(new DateTimeOffset(2026, 3, 2, 4, 30, 0, TimeSpan.Zero));
        Assert.False(await reminder.RunIfDueAsync(CancellationToken.None));
        _f.Time.SetUtcNow(new DateTimeOffset(2026, 3, 2, 5, 30, 0, TimeSpan.Zero));
        Assert.True(await reminder.RunIfDueAsync(CancellationToken.None));
        Assert.False(await reminder.RunIfDueAsync(CancellationToken.None));

        var mail = Assert.Single(Directory.GetFiles(Path.Combine(_f.Folder, "Mail"), "*.eml"));
        var text = File.ReadAllText(mail);
        Assert.Contains("F01 channel 4 Load 1 (F01-TC04) calibration due 2026-03-01 (OVERDUE)", text);

        _f.Time.Advance(TimeSpan.FromDays(1));
        Assert.True(await reminder.RunIfDueAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Released_survey_moves_the_survey_due_date()
    {
        using var db = _f.Db.Create();
        var loads = new LoadService(db, _f.Time);
        var load = await loads.PlanAsync(new PlanLoadRequest("F01", "TUS-480", Array.Empty<PlannedPart>(), "CORP\\q1", LoadKind.Survey));
        load.Status = LoadStatus.Completed;
        load.Passed = true;
        await db.SaveChangesAsync();
        await new LoadService(_f.Db.Create(), _f.Time).DecideAsync(load.LoadNumber, true, "CORP\\q1", null, load.RowVersion);

        using var check = _f.Db.Create();
        Assert.Equal(new DateOnly(2026, 6, 2), (await check.Furnaces.SingleAsync(f => f.Code == "F01")).UniformitySurveyDue);
    }
}
