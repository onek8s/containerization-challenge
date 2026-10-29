using System;
using System.IO;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Integration.Sftp;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Integration.Consumers
{
    /// <summary>Collects order exports from the marketplace SFTP account (intake service).</summary>
    public class MarketplaceOrderPoller
    {
        private readonly ISftpConnector _marketplace;
        private readonly IPublishEndpoint _bus;
        private readonly string _exportDirectory;

        public MarketplaceOrderPoller(ISftpConnector marketplace, IPublishEndpoint bus, string exportDirectory)
        {
            _marketplace = marketplace;
            _bus = bus;
            _exportDirectory = exportDirectory.TrimEnd('/');
        }

        public async Task<int> PollAsync()
        {
            int orders = 0;
            using (var session = Resilience.Sftp("Marketplace orders").Execute(() => _marketplace.Connect()))
            {
                foreach (var path in session.ListFiles(_exportDirectory, ".csv"))
                {
                    byte[] content = session.Download(path);
                    try
                    {
                        var parsed = MarketplaceOrderFile.Read(new MemoryStream(content));
                        foreach (var order in parsed)
                        {
                            await _bus.Publish<OrderSubmitted>(new
                            {
                                OrderId = NewId.NextGuid(),
                                Channel = SalesChannels.Marketplace,
                                ExternalRef = order.OrderRef,
                                SubmittedUtc = DateTime.UtcNow,
                                Order = order.Document
                            });
                        }
                        Log.Information("Marketplace file {File}: {Count} orders", path, parsed.Count);
                        orders += parsed.Count;
                        session.Move(path, _exportDirectory + "/processed");
                    }
                    catch (FormatException ex)
                    {
                        Log.Error(ex, "Marketplace file {File} cannot be read, moved to rejected", path);
                        session.Move(path, _exportDirectory + "/rejected");
                    }
                }
            }
            return orders;
        }
    }
}
