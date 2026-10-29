using System;
using System.Threading;
using System.Threading.Tasks;
using Litware.OrderHub.Integration.Configuration;

namespace Litware.OrderHub.Integration.Partners
{
    /// <summary>Message exchange with Contoso Retail: purchase orders in, dispatch notices out.</summary>
    public interface IPartnerGateway : IDisposable
    {
        /// <summary>Starts delivering inbound partner orders; the handler throws FormatException for a message that can never be processed.</summary>
        void StartReceiving(Func<PartnerMessage, Task> handler);

        Task SendDispatchNoticeAsync(string messageId, string json, CancellationToken cancellationToken);

        Task StopAsync();
    }

    public class PartnerMessage
    {
        public string MessageId { get; set; }
        public string Body { get; set; }
    }

    public static class PartnerGatewayFactory
    {
        /// <summary>Partner.Transport = ServiceBus (default) or Folder.</summary>
        public static IPartnerGateway Create(SettingsReader settings)
        {
            string transport = settings.Get("Partner.Transport", "ServiceBus");
            switch (transport)
            {
                case "ServiceBus":
                    return new ServiceBusPartnerGateway(
                        settings.Require("Partner.ServiceBus.ConnectionString"),
                        settings.Get("Partner.ServiceBus.OrderQueue", "litware-orders"),
                        settings.Get("Partner.ServiceBus.DispatchQueue", "litware-dispatch"));
                case "Folder":
                    return new FolderPartnerGateway(
                        settings.Require("Partner.Folder.Inbound"),
                        settings.Require("Partner.Folder.Outbound"),
                        settings.GetTime("Partner.Folder.PollInterval", TimeSpan.FromSeconds(10)));
                default:
                    throw new InvalidOperationException("Unknown Partner.Transport '" + transport + "'.");
            }
        }
    }
}
