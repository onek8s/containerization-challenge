using Microsoft.AspNetCore.SignalR.Client;

namespace VanArsdel.BackOffice.Services;

/// <summary>Per-circuit SignalR connection to the store hub (through the gateway) for the live stores page.</summary>
public sealed class StoreLiveFeed : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly TokenProvider _tokens;
    private HubConnection _connection;

    public StoreLiveFeed(IConfiguration configuration, TokenProvider tokens)
    {
        _configuration = configuration;
        _tokens = tokens;
    }

    public event Action<string, string> Received;

    public async Task StartAsync()
    {
        if (_connection != null) return;
        var url = _configuration["Gateway:BaseUrl"].TrimEnd('/') + "/hubs/store";
        _connection = new HubConnectionBuilder()
            .WithUrl(url, o => o.AccessTokenProvider = () => Task.FromResult(_tokens.AccessToken))
            .WithAutomaticReconnect()
            .Build();
        foreach (var name in new[] { "salesUpdated", "taskCreated", "collectionReady" })
        {
            _connection.On<System.Text.Json.JsonElement>(name, payload => Received?.Invoke(name, payload.GetRawText()));
        }
        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinAllStores");
    }

    public string State => _connection?.State.ToString() ?? "Disconnected";

    public async ValueTask DisposeAsync()
    {
        if (_connection != null) await _connection.DisposeAsync();
    }
}
