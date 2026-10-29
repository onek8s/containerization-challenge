using Lucerne.Lending.Workflow;

namespace Lucerne.Lending.Api.Infrastructure;

/// <summary>Runs the automatic credit decision for submitted applications.</summary>
public sealed class ScoringWorker : BackgroundService
{
    private readonly ScoringService _scoring;
    private readonly ILogger<ScoringWorker> _log;

    public ScoringWorker(ScoringService scoring, ILogger<ScoringWorker> log)
    {
        _scoring = scoring;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastRetry = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var n = await _scoring.ProcessSubmittedAsync(10, stoppingToken);
                if (DateTime.UtcNow - lastRetry > TimeSpan.FromMinutes(1))
                {
                    await _scoring.RetryFailedAsync(TimeSpan.FromMinutes(5), 6, stoppingToken);
                    lastRetry = DateTime.UtcNow;
                }
                if (n > 0) continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Scoring loop failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}

/// <summary>Books signed applications under the automatic booking limit and expires offers.</summary>
public sealed class BookingWorker : BackgroundService
{
    private readonly BookingService _booking;
    private readonly ApplicationService _applications;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BookingWorker> _log;

    public BookingWorker(BookingService booking, ApplicationService applications, IConfiguration configuration, ILogger<BookingWorker> log)
    {
        _booking = booking;
        _applications = applications;
        _configuration = configuration;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_configuration.GetValue("Lending:BookingIntervalSeconds", 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var booked = await _booking.BookPendingAsync(stoppingToken);
                var expired = await _applications.ExpireOffersAsync(stoppingToken);
                if (booked + expired > 0) _log.LogInformation("Booked {Booked} applications, expired {Expired} offers", booked, expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Booking loop failed");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }
}
