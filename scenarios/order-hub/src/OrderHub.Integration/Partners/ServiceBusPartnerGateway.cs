using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Serilog;

namespace Litware.OrderHub.Integration.Partners
{
    /// <summary>Azure Service Bus namespace owned by Contoso Retail; Litware has a SAS policy with Listen on the order queue and Send on the dispatch queue.</summary>
    public class ServiceBusPartnerGateway : IPartnerGateway
    {
        private readonly QueueClient _orders;
        private readonly QueueClient _dispatch;

        public ServiceBusPartnerGateway(string connectionString, string orderQueue, string dispatchQueue)
        {
            _orders = new QueueClient(connectionString, orderQueue, ReceiveMode.PeekLock, RetryPolicy.Default);
            _dispatch = new QueueClient(connectionString, dispatchQueue, ReceiveMode.PeekLock, RetryPolicy.Default);
        }

        public void StartReceiving(Func<PartnerMessage, Task> handler)
        {
            var options = new MessageHandlerOptions(OnError)
            {
                AutoComplete = false,
                MaxConcurrentCalls = 2,
                MaxAutoRenewDuration = TimeSpan.FromMinutes(5)
            };

            _orders.RegisterMessageHandler(async (message, token) =>
            {
                var partnerMessage = new PartnerMessage
                {
                    MessageId = message.MessageId,
                    Body = Encoding.UTF8.GetString(message.Body)
                };
                try
                {
                    await handler(partnerMessage);
                    await _orders.CompleteAsync(message.SystemProperties.LockToken);
                }
                catch (FormatException ex)
                {
                    Log.Error(ex, "Partner message {MessageId} is not a valid order, dead-lettered", message.MessageId);
                    await _orders.DeadLetterAsync(message.SystemProperties.LockToken, "InvalidOrder", ex.Message);
                }
            }, options);
        }

        private static Task OnError(ExceptionReceivedEventArgs args)
        {
            Log.Error(args.Exception, "Service Bus receive error ({Action} on {Endpoint})",
                args.ExceptionReceivedContext.Action, args.ExceptionReceivedContext.Endpoint);
            return Task.CompletedTask;
        }

        public Task SendDispatchNoticeAsync(string messageId, string json, CancellationToken cancellationToken)
        {
            var message = new Message(Encoding.UTF8.GetBytes(json))
            {
                MessageId = messageId,
                ContentType = "application/json",
                Label = "DispatchNotice"
            };
            return _dispatch.SendAsync(message);
        }

        public async Task StopAsync()
        {
            await _orders.CloseAsync();
            await _dispatch.CloseAsync();
        }

        public void Dispose()
        {
        }
    }
}
