using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VanArsdel.PosFeed.Parsing;
using VanArsdel.PosFeed.Sources;

namespace VanArsdel.PosFeed
{
    /// <summary>Polls the POS drop, publishes one StoreSalesReceived per file, archives or rejects the file.</summary>
    public class PosFeedWorker : BackgroundService
    {
        private readonly IPosFileSource _source;
        private readonly IBus _bus;
        private readonly PosFeedOptions _options;
        private readonly ILogger<PosFeedWorker> _logger;

        public PosFeedWorker(IPosFileSource source, IBus bus, IOptions<PosFeedOptions> options, ILogger<PosFeedWorker> logger)
        {
            _source = source;
            _bus = bus;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Watching {Source} every {Seconds}s", _source.Description, _options.PollSeconds);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Source down (share or SFTP). Try again on the next round.
                    _logger.LogError(ex, "Polling {Source} failed", _source.Description);
                }
                await Task.Delay(TimeSpan.FromSeconds(_options.PollSeconds), stoppingToken).ContinueWith(_ => { });
            }
        }

        public async Task<int> PollOnceAsync(CancellationToken cancellationToken)
        {
            var files = await _source.ListAsync(cancellationToken);
            var published = 0;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Contracts.Pos.StoreSalesReceived message;
                try
                {
                    using (var stream = await _source.OpenAsync(file, cancellationToken))
                    {
                        message = PosFileParser.Parse(file.Name, stream);
                    }
                }
                catch (PosFileException ex)
                {
                    _logger.LogWarning("Rejected {File}: {Reason}", file.Name, ex.Message);
                    await _source.FailAsync(file, ex.Message, cancellationToken);
                    continue;
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Could not read {File}, retrying next round", file.Name);
                    continue;
                }

                await _bus.Publish(message, ctx => ctx.MessageId = message.BatchId, cancellationToken);
                await _source.CompleteAsync(file, cancellationToken);
                published++;
                _logger.LogInformation("Published {File}: {Store} {Date:yyyy-MM-dd} #{Sequence}, {Receipts} receipts",
                    file.Name, message.StoreCode, message.BusinessDate, message.Sequence, message.Receipts.Count);
            }
            return published;
        }
    }
}
