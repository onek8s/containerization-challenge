using System.Threading.Tasks;
using Litware.OrderHub.Contracts;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Integration.Data;
using Litware.OrderHub.Integration.Mail;
using Litware.OrderHub.Integration.Sftp;
using Litware.OrderHub.Integration.Webshop;
using MassTransit;
using Serilog;

namespace Litware.OrderHub.Integration.Consumers
{
    /// <summary>
    /// Reports shipments back to the sales channel (shop sync service, queue orderhub.shopsync.shipments):
    /// webshop orders are completed through the API and the customer gets a shipment e-mail,
    /// marketplace orders get a shipping confirmation file.
    /// </summary>
    public class ShopShipmentConsumer : IConsumer<OrderShipped>
    {
        private readonly IOrderStore _store;
        private readonly WebshopApiClient _webshop;
        private readonly CustomerMailer _mailer;
        private readonly ISftpConnector _marketplace;
        private readonly string _marketplaceConfirmationDirectory;

        public ShopShipmentConsumer(IOrderStore store, WebshopApiClient webshop, CustomerMailer mailer,
            ISftpConnector marketplace, string marketplaceConfirmationDirectory)
        {
            _store = store;
            _webshop = webshop;
            _mailer = mailer;
            _marketplace = marketplace;
            _marketplaceConfirmationDirectory = marketplaceConfirmationDirectory;
        }

        public async Task Consume(ConsumeContext<OrderShipped> context)
        {
            var m = context.Message;
            string trackingUrl = CarrierLinks.TrackingUrl(m.Carrier, m.TrackingNumber);

            if (m.Channel == SalesChannels.Webshop)
            {
                await _webshop.CompleteOrderAsync(m.ExternalRef, m.Carrier, m.TrackingNumber, trackingUrl, context.CancellationToken);
                await _store.AddEventAsync(m.OrderId, "WebshopCompleted", "Order " + m.ExternalRef);

                var order = await _store.FindAsync(m.OrderId);
                if (!string.IsNullOrEmpty(order?.Document.CustomerEmail))
                {
                    string language = CustomerMailer.LanguageFor(order.Document.ShipTo?.CountryCode);
                    var mail = _mailer.Render("shipped", language, order.Document.CustomerEmail, new
                    {
                        name = order.Document.CustomerName,
                        orderNumber = order.Document.CustomerReference,
                        carrier = m.Carrier,
                        trackingNumber = m.TrackingNumber,
                        trackingUrl,
                        lines = order.Document.Lines
                    });
                    await _mailer.SendAsync(mail, context.CancellationToken);
                    await _store.AddEventAsync(m.OrderId, "ShipmentMailSent", order.Document.CustomerEmail);
                }
            }
            else if (m.Channel == SalesChannels.Marketplace && _marketplace != null)
            {
                byte[] file = MarketplaceShipmentFile.Build(m.ExternalRef, m.Carrier, m.TrackingNumber, m.ShippedUtc, m.Lines);
                Resilience.Sftp("Marketplace confirmation " + m.OrderNumber).Execute(() =>
                {
                    using (var session = _marketplace.Connect())
                    {
                        session.Upload(_marketplaceConfirmationDirectory, MarketplaceShipmentFile.FileName(m.OrderNumber), file);
                    }
                });
                await _store.AddEventAsync(m.OrderId, "MarketplaceConfirmed", MarketplaceShipmentFile.FileName(m.OrderNumber));
            }
            else
            {
                return;
            }

            Log.Information("Shipment of {OrderNumber} reported to {Channel}", m.OrderNumber, m.Channel);
        }
    }

    /// <summary>Puts rejected webshop orders on hold so customer service sees them in the shop admin.</summary>
    public class ShopRejectionConsumer : IConsumer<OrderRejected>
    {
        private readonly WebshopApiClient _webshop;

        public ShopRejectionConsumer(WebshopApiClient webshop)
        {
            _webshop = webshop;
        }

        public async Task Consume(ConsumeContext<OrderRejected> context)
        {
            if (context.Message.Channel != SalesChannels.Webshop) return;
            await _webshop.HoldOrderAsync(context.Message.ExternalRef, context.Message.Reason, context.CancellationToken);
            Log.Information("Webshop order {ExternalRef} put on hold", context.Message.ExternalRef);
        }
    }
}
