namespace Humongous.Quoting.Messaging;

public sealed class BusOptions
{
    public const string SectionName = "Bus";

    /// <summary><c>RabbitMq</c>, or <c>InMemory</c> for tests and single-process tools.</summary>
    public string Transport { get; set; } = "RabbitMq";

    public string Host { get; set; } = "mq01.corp.local";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "humongous";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseTls { get; set; }

    /// <summary>Prefix for the queue names of this application, e.g. <c>policy-service</c>.</summary>
    public string EndpointPrefix { get; set; } = "";

    public int PrefetchCount { get; set; } = 8;
}
