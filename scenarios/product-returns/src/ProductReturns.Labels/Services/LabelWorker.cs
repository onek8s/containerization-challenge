using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProductReturns.Labels.Services
{
    public class LabelWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly LabelOptions _options;
        private readonly ILogger<LabelWorker> _logger;

        public LabelWorker(IServiceScopeFactory scopes, IOptions<LabelOptions> options, ILogger<LabelWorker> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Label worker started, polling every {Seconds} s, labels to {Share}", _options.PollSeconds, _options.SharePath);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopes.CreateScope())
                    {
                        var processor = scope.ServiceProvider.GetRequiredService<LabelProcessor>();
                        var count = await processor.RunOnceAsync(stoppingToken);
                        if (count > 0)
                        {
                            _logger.LogInformation("{Count} return labels issued", count);
                        }
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError(ex, "Label run failed");
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.PollSeconds)), stoppingToken);
            }
        }
    }
}
