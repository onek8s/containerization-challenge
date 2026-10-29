using System;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Core.Partner;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Integration.Partners
{
    /// <summary>Turns partner purchase orders into hub orders (partners service).</summary>
    public class PartnerOrderReceiver
    {
        private readonly IPublishEndpoint _bus;

        public PartnerOrderReceiver(IPublishEndpoint bus)
        {
            _bus = bus;
        }

        public async Task HandleAsync(PartnerMessage message)
        {
            PartnerOrder order;
            try
            {
                order = PartnerOrder.Parse(message.Body);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new FormatException("Partner message " + message.MessageId + " is not valid JSON.", ex);
            }

            await _bus.Publish<OrderSubmitted>(new
            {
                OrderId = NewId.NextGuid(),
                Channel = SalesChannels.Partner,
                ExternalRef = order.PoNumber,
                SubmittedUtc = DateTime.UtcNow,
                Order = order.ToDocument()
            });
            Log.Information("Partner order {PoNumber} received ({Lines} lines)", order.PoNumber, order.Lines?.Count ?? 0);
        }
    }
}
