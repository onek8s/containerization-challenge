using System.Net.Http.Headers;
using System.Text;
using FurnaceRecords.Data;
using Microsoft.EntityFrameworkCore;

namespace FurnaceRecords.Worker;

/// <summary>Posts load events from the outbox to MES, oldest first, with back-off on failure.</summary>
internal sealed class OutboxDispatcher : BackgroundService
{
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _http;
    private readonly WorkerOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceScopeFactory scopes, IHttpClientFactory http, WorkerOptions options, TimeProvider time, ILogger<OutboxDispatcher> logger)
    {
        _scopes = scopes;
        _http = http;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox dispatch failed");
            }
            await Task.Delay(Poll, _time, stoppingToken);
        }
    }

    internal async Task<int> DispatchAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FurnaceRecordsContext>();
        var now = _time.GetUtcNow().UtcDateTime;
        var batch = await db.Outbox
            .Where(m => m.SentAtUtc == null && m.NextAttemptUtc <= now)
            .OrderBy(m => m.Id)
            .Take(_options.OutboxBatchSize)
            .ToListAsync(ct);

        // Events of one load are delivered in order: skip a message while an older one of the same load is unsent.
        var keys = batch.Select(m => m.Key).Distinct().ToList();
        var oldestUnsent = await db.Outbox
            .Where(m => m.SentAtUtc == null && keys.Contains(m.Key))
            .GroupBy(m => m.Key)
            .Select(g => new { Key = g.Key, Id = g.Min(m => m.Id) })
            .ToDictionaryAsync(x => x.Key, x => x.Id, ct);

        var client = _http.CreateClient("mes");
        var sent = 0;
        var blocked = new HashSet<string>();
        foreach (var message in batch)
        {
            if (blocked.Contains(message.Key) || (oldestUnsent.TryGetValue(message.Key, out var oldest) && oldest < message.Id && !batch.Any(m => m.Id == oldest && m.SentAtUtc is not null)))
            {
                blocked.Add(message.Key);
                continue;
            }
            try
            {
                using var content = new StringContent(message.PayloadJson, Encoding.UTF8, "application/json");
                content.Headers.Add("X-Event-Id", message.Id.ToString());
                using var response = await client.PostAsync($"events/{message.Topic}", content, ct);
                response.EnsureSuccessStatusCode();
                message.SentAtUtc = _time.GetUtcNow().UtcDateTime;
                message.LastError = null;
                sent++;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                message.NextAttemptUtc = _time.GetUtcNow().UtcDateTime.Add(Backoff(message.Attempts));
                blocked.Add(message.Key);
                _logger.LogWarning("MES event {Topic} for {Key} failed (attempt {Attempts}): {Error}", message.Topic, message.Key, message.Attempts, ex.Message);
            }
        }
        await db.SaveChangesAsync(ct);
        return sent;
    }

    internal static TimeSpan Backoff(int attempts) => TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Min(attempts, 6)) / 2));
}
