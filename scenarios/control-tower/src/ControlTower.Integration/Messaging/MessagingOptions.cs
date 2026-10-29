namespace Proseware.ControlTower.Integration.Messaging;

public sealed class MessagingOptions
{
    public const string Section = "Messaging";

    /// <summary>RabbitMq (default) or InMemory (development and tests).</summary>
    public string Transport { get; set; } = "RabbitMq";

    /// <summary>Prefix for the subscription (queue) names of this installation, e.g. "prod" or "test".</summary>
    public string Environment { get; set; } = "prod";

    public ushort PrefetchCount { get; set; } = 20;
}
