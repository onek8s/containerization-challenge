using System;
using Newtonsoft.Json;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Sales
{
    public class CancelPayload
    {
        public string Reason { get; set; }
    }

    public class ConvertQuotePayload
    {
        public string CustomerRef { get; set; }
    }

    /// <summary>Creates AX commands. Commands of one order share a sequence key so they run in order.</summary>
    public class AxCommandFactory
    {
        private readonly IAxCommandQueue _queue;
        private readonly IClock _clock;

        public AxCommandFactory(IAxCommandQueue queue, IClock clock)
        {
            _queue = queue;
            _clock = clock;
        }

        public AxCommand ForOrder(AxCommandType type, SalesOrder order, string requestedBy, object payload = null, string runAs = null) =>
            Enqueue(new AxCommand
            {
                CommandType = type,
                OrderId = order.Id,
                DealerId = order.DealerId,
                QuoteId = order.QuoteId,
                SequenceKey = "order:" + order.Id,
                RequestedBy = requestedBy,
                RunAsAxUser = runAs,
                Payload = payload == null ? null : JsonConvert.SerializeObject(payload)
            });

        public AxCommand ForQuote(AxCommandType type, Quote quote, string requestedBy) =>
            Enqueue(new AxCommand
            {
                CommandType = type,
                QuoteId = quote.Id,
                DealerId = quote.DealerId,
                SequenceKey = "quote:" + quote.Id,
                RequestedBy = requestedBy
            });

        public AxCommand ForDealer(Dealer dealer, string requestedBy) =>
            Enqueue(new AxCommand
            {
                CommandType = AxCommandType.SyncDealer,
                DealerId = dealer.Id,
                SequenceKey = "dealer:" + dealer.Id,
                RequestedBy = requestedBy
            });

        private AxCommand Enqueue(AxCommand command)
        {
            command.State = AxCommandState.Pending;
            command.CreatedUtc = _clock.UtcNow;
            command.NextAttemptUtc = _clock.UtcNow;
            _queue.Enqueue(command);
            return command;
        }

        public static T Payload<T>(AxCommand command) where T : new() =>
            string.IsNullOrEmpty(command.Payload) ? new T() : JsonConvert.DeserializeObject<T>(command.Payload);
    }
}
