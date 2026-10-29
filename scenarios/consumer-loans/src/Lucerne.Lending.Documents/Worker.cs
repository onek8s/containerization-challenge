using Lucerne.Lending.Contracts;
using Lucerne.Lending.Data;

namespace Lucerne.Lending.Documents;

/// <summary>Processes document, signing callback and notification work from the outbox.</summary>
public sealed class Worker : BackgroundService
{
    private static readonly string[] Handled = { Topics.DocumentRequested, Topics.SigningCallback, Topics.CustomerNotification };
    private readonly OutboxRepository _outbox;
    private readonly DocumentJobs _jobs;
    private readonly SigningCompletion _signing;
    private readonly Notifications _notifications;
    private readonly ILogger<Worker> _log;
    private readonly string _workerId = $"{Environment.MachineName}/documents/{Environment.ProcessId}";

    public Worker(OutboxRepository outbox, DocumentJobs jobs, SigningCompletion signing, Notifications notifications, ILogger<Worker> log)
    {
        _outbox = outbox;
        _jobs = jobs;
        _signing = signing;
        _notifications = notifications;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastPoll = DateTime.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = Array.Empty<OutboxMessage>() as IReadOnlyList<OutboxMessage>;
            try
            {
                batch = await _outbox.ClaimAsync(Handled, _workerId, 10, TimeSpan.FromMinutes(5), stoppingToken);
                foreach (var message in batch) await ProcessAsync(message, stoppingToken);
                if (DateTime.UtcNow - lastPoll > TimeSpan.FromMinutes(15))
                {
                    await _signing.PollAsync(stoppingToken);
                    lastPoll = DateTime.UtcNow;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Documents loop failed");
            }
            if (batch.Count == 0) await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task ProcessAsync(OutboxMessage message, CancellationToken ct)
    {
        try
        {
            switch (message.Topic)
            {
                case Topics.DocumentRequested:
                    await _jobs.HandleAsync(OutboxRepository.Read<DocumentRequest>(message), ct);
                    break;
                case Topics.SigningCallback:
                    await _signing.HandleCallbackAsync(OutboxRepository.Read<SigningCallback>(message), ct);
                    break;
                case Topics.CustomerNotification:
                    await _notifications.SendAsync(OutboxRepository.Read<CustomerNotification>(message), ct);
                    break;
            }
            await _outbox.CompleteAsync(message.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var delay = TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, message.Attempts)));
            _log.LogError(ex, "Outbox message {Id} ({Topic}) failed, attempt {Attempt}, retry in {Delay}", message.Id, message.Topic, message.Attempts, delay);
            await _outbox.FailAsync(message.Id, ex.Message, delay);
        }
    }
}
