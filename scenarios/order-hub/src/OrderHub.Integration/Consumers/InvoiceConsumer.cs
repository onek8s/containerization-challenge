using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Reporting;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Integration.Consumers
{
    /// <summary>Invoices shipped orders and stores the invoice PDF (partners service, queue orderhub.partners.invoicing).</summary>
    public class InvoiceConsumer : IConsumer<OrderShipped>
    {
        private readonly IOrderStore _store;
        private readonly IInvoiceRenderer _renderer;

        public InvoiceConsumer(IOrderStore store, IInvoiceRenderer renderer)
        {
            _store = store;
            _renderer = renderer;
        }

        public async Task Consume(ConsumeContext<OrderShipped> context)
        {
            var message = context.Message;
            var order = await _store.FindAsync(message.OrderId);
            if (order == null) return;

            bool alreadyInvoiced = order.InvoiceNumber != null;

            // The invoice number must exist before rendering: the report reads it from the database.
            string invoiceNumber = await _store.SaveInvoiceAsync(order.OrderId, null);
            byte[] pdf = await _renderer.RenderAsync(order.OrderNumber, context.CancellationToken);
            if (pdf != null)
            {
                await _store.SaveInvoiceAsync(order.OrderId, pdf);
            }

            if (alreadyInvoiced)
            {
                Log.Information("Order {OrderNumber} already invoiced as {InvoiceNumber}, document refreshed", order.OrderNumber, invoiceNumber);
                return;
            }

            await _store.AddEventAsync(order.OrderId, "Invoiced", invoiceNumber + (pdf == null ? " (no document)" : ""));
            await context.Publish<OrderInvoiced>(new { order.OrderId, order.OrderNumber, InvoiceNumber = invoiceNumber });
            Log.Information("Order {OrderNumber} invoiced as {InvoiceNumber}", order.OrderNumber, invoiceNumber);
        }
    }
}
