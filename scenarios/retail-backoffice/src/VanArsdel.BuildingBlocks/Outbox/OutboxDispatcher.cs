using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VanArsdel.BuildingBlocks.Caching;

namespace VanArsdel.BuildingBlocks.Outbox
{
    /// <summary>
    /// Publishes outbox rows in order. Only one instance per service sends at a time (distributed lock),
    /// because both app hosts run every service.
    /// </summary>
    public class OutboxDispatcher<TContext> : BackgroundService where TContext : DbContext, IOutboxContext
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDistributedLockProvider _locks;
        private readonly ILogger<OutboxDispatcher<TContext>> _logger;
        private readonly TimeSpan _interval;
        private readonly int _batchSize;

        public OutboxDispatcher(IServiceScopeFactory scopeFactory, IDistributedLockProvider locks, IConfiguration configuration,
            ILogger<OutboxDispatcher<TContext>> logger)
        {
            _scopeFactory = scopeFactory;
            _locks = locks;
            _logger = logger;
            _interval = TimeSpan.FromMilliseconds(configuration.GetValue("Outbox:PollIntervalMs", 2000));
            _batchSize = configuration.GetValue("Outbox:BatchSize", 50);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the bus start first.
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            var resource = "outbox:" + typeof(TContext).Name;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var handle = await _locks.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), TimeSpan.Zero, stoppingToken);
                    if (handle != null)
                    {
                        int sent;
                        do
                        {
                            sent = await DispatchBatchAsync(stoppingToken);
                        }
                        while (sent == _batchSize && !stoppingToken.IsCancellationRequested);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox dispatch failed");
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            var batch = await db.OutboxMessages
                .Where(m => m.SentUtc == null && m.Attempts < 20)
                .OrderBy(m => m.Id)
                .Take(_batchSize)
                .ToListAsync(cancellationToken);

            foreach (var message in batch)
            {
                try
                {
                    var type = Type.GetType(message.MessageType, throwOnError: true);
                    var body = JsonConvert.DeserializeObject(message.Body, type);
                    await publish.Publish(body, type, ctx =>
                    {
                        ctx.MessageId = message.MessageId;
                        if (Guid.TryParse(message.CorrelationId, out var correlationId)) ctx.CorrelationId = correlationId;
                    }, cancellationToken);
                    message.SentUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    message.Attempts++;
                    message.LastError = ex.Message.Length > 2000 ? ex.Message.Substring(0, 2000) : ex.Message;
                    _logger.LogWarning(ex, "Outbox message {MessageId} ({Type}) failed, attempt {Attempts}", message.MessageId, message.MessageType, message.Attempts);
                    break;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            if (batch.Count > 0)
            {
                _logger.LogDebug("Outbox sent {Count} messages", batch.Count(m => m.SentUtc != null));
            }
            return batch.Count;
        }
    }

    public static class OutboxServiceCollectionExtensions
    {
        public static IServiceCollection AddEfOutbox<TContext>(this IServiceCollection services) where TContext : DbContext, IOutboxContext
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IOutbox, EfOutbox<TContext>>();
            services.AddHostedService<OutboxDispatcher<TContext>>();
            return services;
        }
    }
}
