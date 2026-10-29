using System.Threading.Channels;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;

namespace Proseware.ControlTower.Telematics;

/// <summary>
/// Persistent MQTT session with the fleet broker. The broker queues QoS 1 messages for the client id
/// while the service is down, so nothing is lost during restarts.
/// </summary>
public sealed class MqttIngestService : BackgroundService
{
    private readonly TelematicsOptions _options;
    private readonly ChannelWriter<TelematicsReading> _writer;
    private readonly ILogger<MqttIngestService> _logger;
    private IManagedMqttClient? _client;
    private long _received;
    private long _rejected;

    public MqttIngestService(IOptions<TelematicsOptions> options, Channel<TelematicsReading> channel, ILogger<MqttIngestService> logger)
    {
        _options = options.Value;
        _writer = channel.Writer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
            .WithCleanSession(false)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));
        if (!string.IsNullOrEmpty(_options.UserName)) clientOptions.WithCredentials(_options.UserName, _options.Password);
        if (_options.UseTls) clientOptions.WithTls();

        var managed = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(10))
            .WithClientOptions(clientOptions.Build())
            .Build();

        _client = new MqttFactory().CreateManagedMqttClient();
        _client.UseConnectedHandler(_ => _logger.LogInformation("Connected to {Host}:{Port} as {ClientId}", _options.BrokerHost, _options.BrokerPort, _options.ClientId));
        _client.UseDisconnectedHandler(e => _logger.LogWarning(e.Exception, "Disconnected from MQTT broker: {Reason}", e.Reason));
        _client.UseApplicationMessageReceivedHandler(async e =>
        {
            Interlocked.Increment(ref _received);
            try
            {
                var reading = TelematicsParser.Parse(e.ApplicationMessage.Topic, e.ApplicationMessage.Payload ?? Array.Empty<byte>());
                if (reading is null)
                {
                    Interlocked.Increment(ref _rejected);
                    return;
                }
                await _writer.WriteAsync(reading, stoppingToken);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _rejected);
                _logger.LogDebug(ex, "Bad payload on {Topic}", e.ApplicationMessage.Topic);
            }
        });

        var root = _options.TopicRoot.TrimEnd('/');
        await _client.SubscribeAsync(
            new MqttTopicFilterBuilder().WithTopic($"{root}/+/position").WithAtLeastOnceQoS().Build(),
            new MqttTopicFilterBuilder().WithTopic($"{root}/+/reefer").WithAtLeastOnceQoS().Build());
        await _client.StartAsync(managed);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                _logger.LogInformation("Telematics messages received {Received}, rejected {Rejected}", Interlocked.Read(ref _received), Interlocked.Read(ref _rejected));
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            await _client.StopAsync();
            _client.Dispose();
        }
        _writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }
}
