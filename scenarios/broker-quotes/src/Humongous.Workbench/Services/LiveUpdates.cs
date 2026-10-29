using Humongous.Quoting.Contracts;
using Humongous.Quoting.Contracts.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Humongous.Workbench.Services;

/// <summary>Circuit-scoped connection to the quote API hub, so referral lists refresh by themselves.</summary>
public sealed class LiveUpdates(IConfiguration configuration, UserTokens tokens, ILogger<LiveUpdates> logger) : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Func<QuoteNotice, Task>? QuoteChanged;
    public event Func<PolicyNotice, Task>? PolicyChanged;

    public async Task EnsureStartedAsync()
    {
        if (_connection is not null) return;
        var url = new Uri(new Uri(configuration["QuoteApi:BaseAddress"]!), "hubs/quotes");
        _connection = new HubConnectionBuilder()
            .WithUrl(url, options => options.AccessTokenProvider = () => tokens.GetAccessTokenAsync())
            .WithAutomaticReconnect()
            .AddJsonProtocol(options => QuotingJson.Configure(options.PayloadSerializerOptions))
            .Build();
        _connection.On<QuoteNotice>(nameof(IQuoteHubClient.QuoteChanged), n => QuoteChanged?.Invoke(n) ?? Task.CompletedTask);
        _connection.On<PolicyNotice>(nameof(IQuoteHubClient.PolicyChanged), n => PolicyChanged?.Invoke(n) ?? Task.CompletedTask);
        try
        {
            await _connection.StartAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Live updates from the quote API are not available");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
