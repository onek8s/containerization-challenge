using Humongous.Quoting.Api.Infrastructure;
using Humongous.Quoting.Contracts.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Humongous.Quoting.Api.Hubs;

/// <summary>Live quote, policy and document updates for the broker portal and the workbench.</summary>
[Authorize(Policy = Policies.AnyUser)]
public sealed class QuoteHub(ILogger<QuoteHub> logger) : Hub<IQuoteHubClient>
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        if (user.IsUnderwriter())
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Underwriters);
        if (user.AgencyCode() is { } agency)
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Agency(agency));
        logger.LogDebug("Hub connection {Connection} for {User}", Context.ConnectionId, user.DisplayName());
        await base.OnConnectedAsync();
    }

    /// <summary>Follow one quote, e.g. while the broker waits on the quote page.</summary>
    public Task WatchQuote(Guid quoteId) => Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Quote(quoteId));

    public Task UnwatchQuote(Guid quoteId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.Quote(quoteId));
}

public sealed class HubNotifier(IHubContext<QuoteHub, IQuoteHubClient> hub)
{
    public Task QuoteChanged(QuoteNotice notice) =>
        hub.Clients.Groups(HubGroups.Agency(notice.AgencyCode), HubGroups.Underwriters, HubGroups.Quote(notice.QuoteId))
            .QuoteChanged(notice);

    public Task PolicyChanged(PolicyNotice notice) =>
        hub.Clients.Groups(HubGroups.Agency(notice.AgencyCode), HubGroups.Underwriters).PolicyChanged(notice);

    public Task DocumentReady(string agencyCode, DocumentNotice notice) =>
        hub.Clients.Groups(HubGroups.Agency(agencyCode), HubGroups.Underwriters).DocumentReady(notice);
}
