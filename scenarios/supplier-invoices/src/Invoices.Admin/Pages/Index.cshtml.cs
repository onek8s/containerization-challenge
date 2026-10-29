using Microsoft.AspNetCore.Mvc.RazorPages;
using Wingtip.Invoices.Admin.Services;
using Wingtip.Invoices.Contracts.Control;

namespace Wingtip.Invoices.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly HubData data;
    private readonly ProcessorClient processor;

    public IndexModel(HubData data, ProcessorClient processor)
    {
        this.data = data;
        this.processor = processor;
    }

    public IList<StatusCount> Counts { get; private set; }
    public IList<IssueCount> Issues { get; private set; }
    public IList<ChannelRow> Channels { get; private set; }
    public IList<QueueStatus> Queues { get; private set; } = Array.Empty<QueueStatus>();
    public string QueueError { get; private set; }

    public async Task OnGetAsync()
    {
        Counts = await data.StatusCountsAsync();
        Issues = await data.OpenIssueCountsAsync();
        Channels = await data.ChannelsAsync();
        try
        {
            Queues = await processor.GetQueueStatusAsync();
        }
        catch (ProcessorException ex)
        {
            QueueError = ex.Message;
        }
    }
}
