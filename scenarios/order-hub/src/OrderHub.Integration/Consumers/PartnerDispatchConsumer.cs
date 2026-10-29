using System.Linq;
using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Core.Partner;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Partners;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Integration.Consumers
{
    /// <summary>Sends the dispatch notice of invoiced partner orders (partners service, queue orderhub.partners.dispatch).</summary>
    public class PartnerDispatchConsumer : IConsumer<OrderInvoiced>
    {
        private readonly IOrderStore _store;
        private readonly IPartnerGateway _partner;

        public PartnerDispatchConsumer(IOrderStore store, IPartnerGateway partner)
        {
            _store = store;
            _partner = partner;
        }

        public async Task Consume(ConsumeContext<OrderInvoiced> context)
        {
            var order = await _store.FindAsync(context.Message.OrderId);
            if (order == null || order.Channel != SalesChannels.Partner) return;

            var notice = new DispatchNotice
            {
                PoNumber = DispatchNotice.PoFromCustomerReference(order.Document.CustomerReference),
                SupplierOrderNo = order.OrderNumber,
                InvoiceNo = order.InvoiceNumber,
                Carrier = order.Carrier,
                TrackingNo = order.TrackingNumber,
                ShippedAtUtc = order.ShippedUtc ?? order.UpdatedUtc,
                Lines = order.Document.Lines.Select(l => new DispatchLine { ArticleNo = l.Sku, Qty = l.Quantity }).ToList()
            };

            await _partner.SendDispatchNoticeAsync(order.OrderNumber, notice.ToJson(), context.CancellationToken);
            await _store.AddEventAsync(order.OrderId, "DispatchNoticeSent", "PO " + notice.PoNumber);
            Log.Information("Dispatch notice for PO {PoNumber} ({OrderNumber}) sent", notice.PoNumber, order.OrderNumber);
        }
    }
}
