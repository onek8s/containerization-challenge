using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Newtonsoft.Json;
using Tailspin.DealerOrders.Contracts.Ax;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;

namespace Tailspin.DealerOrders.Processing.Ax
{
    /// <summary>
    /// Executes queued AX commands against the gateways. Commands are idempotent: an order that already has an AX sales
    /// id is not created again, and the order number is sent to AX as PurchOrderFormNum so the X++ side can detect a
    /// repeat after a lost reply.
    /// </summary>
    public class AxCommandProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AxCommandProcessor));

        private readonly IAxCommandQueue _queue;
        private readonly IAxSalesGateway _sales;
        private readonly IAxDocumentReader _reader;
        private readonly IOrderStore _orders;
        private readonly IQuoteStore _quotes;
        private readonly IDealerStore _dealers;
        private readonly AxDocumentMapper _mapper;
        private readonly OrderStatusSync _statusSync;
        private readonly NotificationComposer _notifications;
        private readonly IClock _clock;
        private readonly ProcessingSettings _settings;

        public AxCommandProcessor(IAxCommandQueue queue, IAxSalesGateway sales, IAxDocumentReader reader, IOrderStore orders, IQuoteStore quotes,
            IDealerStore dealers, AxDocumentMapper mapper, OrderStatusSync statusSync, NotificationComposer notifications, IClock clock, ProcessingSettings settings)
        {
            _queue = queue;
            _sales = sales;
            _reader = reader;
            _orders = orders;
            _quotes = quotes;
            _dealers = dealers;
            _mapper = mapper;
            _statusSync = statusSync;
            _notifications = notifications;
            _clock = clock;
            _settings = settings;
        }

        /// <summary>Claims and runs due commands until none are left or <paramref name="max"/> have run. Returns the count.</summary>
        public int RunDue(int max)
        {
            var count = 0;
            while (count < max)
            {
                var command = _queue.ClaimNext(_clock.UtcNow);
                if (command == null) break;
                Execute(command);
                count++;
            }
            return count;
        }

        public void Execute(AxCommand command)
        {
            var context = new AxCallContext
            {
                Company = _settings.AxCompany,
                Language = _settings.AxLanguage,
                RunAsUser = command.RunAsAxUser,
                MessageId = "dlr-cmd-" + command.Id + "-" + (command.Attempts + 1)
            };
            command.Attempts++;
            var started = DateTime.UtcNow;
            try
            {
                command.Result = Run(command, context);
                command.State = AxCommandState.Done;
                command.CompletedUtc = _clock.UtcNow;
                command.LastError = null;
                Log.InfoFormat("AX command {0} {1} done in {2:0} ms as {3}: {4}", command.Id, command.CommandType, (DateTime.UtcNow - started).TotalMilliseconds, context, command.Result);
            }
            catch (AxUnavailableException ex)
            {
                command.LastError = ex.Message;
                if (command.Attempts >= _settings.AxMaxAttempts)
                {
                    command.State = AxCommandState.Failed;
                    Log.Error($"AX command {command.Id} {command.CommandType} gave up after {command.Attempts} attempts", ex);
                    MarkError(command, "AX was not reachable: " + ex.Message);
                }
                else
                {
                    var delays = _settings.AxRetryDelays;
                    var delay = delays[Math.Min(command.Attempts - 1, delays.Length - 1)];
                    command.State = AxCommandState.Retry;
                    command.NextAttemptUtc = _clock.UtcNow.Add(delay);
                    Log.WarnFormat("AX command {0} {1} will be retried at {2:u}: {3}", command.Id, command.CommandType, command.NextAttemptUtc, ex.Message);
                }
            }
            catch (AxBusinessException ex)
            {
                command.State = AxCommandState.Failed;
                command.LastError = ex.Message;
                Log.Error($"AX refused command {command.Id} {command.CommandType}", ex);
                MarkError(command, ex.Message);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is KeyNotFoundException || ex is JsonException)
            {
                command.State = AxCommandState.Failed;
                command.LastError = ex.Message;
                Log.Error($"AX command {command.Id} {command.CommandType} failed", ex);
                MarkError(command, ex.Message);
            }
            _queue.Save(command);
        }

        private string Run(AxCommand command, AxCallContext context)
        {
            switch (command.CommandType)
            {
                case AxCommandType.SyncDealer: return SyncDealer(command, context);
                case AxCommandType.RegisterQuote: return RegisterQuote(command, context);
                case AxCommandType.ConvertQuote: return ConvertQuote(command, context);
                case AxCommandType.CreateOrder: return CreateOrder(command, context);
                case AxCommandType.UpdateOrder: return UpdateOrder(command, context);
                case AxCommandType.CancelOrder: return CancelOrder(command, context);
                case AxCommandType.ReserveStock: return Reserve(command, context);
                case AxCommandType.ReleaseReservation: return Release(command, context);
                case AxCommandType.ReadOrder: return ReadOrder(command, context);
                default: throw new InvalidOperationException($"Unknown command type {command.CommandType}.");
            }
        }

        private string SyncDealer(AxCommand command, AxCallContext context)
        {
            var dealer = _dealers.Get(command.DealerId.Value);
            var result = _sales.EnsureCustomer(AxDocumentMapper.Customer(dealer), context);
            foreach (var address in dealer.Addresses)
                if (result.LocationIdsByExternalCode.TryGetValue("ADDR-" + address.Id, out var locationId)) address.AxLocationId = locationId;
            dealer.LastSyncedUtc = _clock.UtcNow;
            _dealers.Save(dealer);
            return (result.Created ? "created " : "updated ") + result.AccountNum;
        }

        private string RegisterQuote(AxCommand command, AxCallContext context)
        {
            var quote = _quotes.Get(command.QuoteId.Value);
            if (!string.IsNullOrEmpty(quote.AxQuotationId)) return "already registered as " + quote.AxQuotationId;
            if (quote.Status != QuoteStatus.Approved) return "skipped, quote is " + quote.Status;
            var dealer = _dealers.Get(quote.DealerId);
            EnsureDealer(dealer, context);
            var document = _mapper.Quotation(quote, dealer);
            EnsureConfigurations(document, context);
            var result = _sales.CreateQuotation(document, context);
            quote.AxQuotationId = result.DocumentId;
            quote.Status = QuoteStatus.RegisteredInAx;
            _quotes.Save(quote);
            _notifications.QuoteEvent(NotificationKind.QuoteApproved, quote, dealer, null);
            return "quotation " + result.DocumentId;
        }

        private string ConvertQuote(AxCommand command, AxCallContext context)
        {
            var order = _orders.Get(command.OrderId.Value);
            if (!string.IsNullOrEmpty(order.AxSalesId)) return "already created as " + order.AxSalesId;
            var quote = _quotes.Get(order.QuoteId.Value);
            var payload = AxCommandFactory.Payload<ConvertQuotePayload>(command);
            var result = _sales.ConvertQuotation(quote.AxQuotationId, payload.CustomerRef, context);
            ApplyCreated(order, result, command);
            quote.Status = QuoteStatus.Converted;
            _quotes.Save(quote);
            return "quotation " + quote.AxQuotationId + " converted to " + result.DocumentId;
        }

        private string CreateOrder(AxCommand command, AxCallContext context)
        {
            var order = _orders.Get(command.OrderId.Value);
            if (!string.IsNullOrEmpty(order.AxSalesId)) return "already created as " + order.AxSalesId;
            if (order.Status == OrderStatus.Cancelled) return "skipped, order was cancelled";
            var dealer = _dealers.Get(order.DealerId);
            EnsureDealer(dealer, context);
            if (order.DeliveryAddress == null && order.DeliveryAddressId.HasValue)
                order.DeliveryAddress = dealer.Addresses.FirstOrDefault(a => a.Id == order.DeliveryAddressId.Value);
            var document = _mapper.Order(order, dealer);
            EnsureConfigurations(document, context);
            var result = _sales.CreateSalesOrder(document, context);
            ApplyCreated(order, result, command);
            return "sales order " + result.DocumentId;
        }

        private void ApplyCreated(SalesOrder order, AxDocumentResult result, AxCommand command)
        {
            order.AxSalesId = result.DocumentId;
            foreach (var line in order.Lines)
                if (result.InventTransIdsByLine.TryGetValue(line.LineNumber, out var transId)) line.AxInventTransId = transId;
            order.ChangeStatus(OrderStatus.CreatedInAx, command.RequestedBy ?? "ax-connector", "AX order " + result.DocumentId, _clock.UtcNow);
            _orders.Save(order);

            var read = new AxCommand
            {
                CommandType = AxCommandType.ReadOrder,
                OrderId = order.Id,
                DealerId = order.DealerId,
                SequenceKey = command.SequenceKey,
                RequestedBy = "ax-connector",
                State = AxCommandState.Pending,
                CreatedUtc = _clock.UtcNow,
                NextAttemptUtc = _clock.UtcNow
            };
            _queue.Enqueue(read);
        }

        private string UpdateOrder(AxCommand command, AxCallContext context)
        {
            var order = _orders.Get(command.OrderId.Value);
            var dealer = _dealers.Get(order.DealerId);
            var document = _mapper.Order(order, dealer);
            EnsureConfigurations(document, context);
            var result = _sales.UpdateSalesOrder(order.AxSalesId, document, context);
            foreach (var line in order.Lines)
                if (result.InventTransIdsByLine.TryGetValue(line.LineNumber, out var transId)) line.AxInventTransId = transId;
            _orders.Save(order);
            return "updated " + order.AxSalesId;
        }

        private string CancelOrder(AxCommand command, AxCallContext context)
        {
            var order = _orders.Get(command.OrderId.Value);
            var payload = AxCommandFactory.Payload<CancelPayload>(command);
            if (!string.IsNullOrEmpty(order.AxSalesId)) _sales.CancelSalesOrder(order.AxSalesId, payload.Reason, context);
            order.ChangeStatus(OrderStatus.Cancelled, command.RequestedBy, payload.Reason, _clock.UtcNow);
            foreach (var line in order.Lines) { line.Reservation = ReservationStatus.Released; line.ReservedQuantity = 0; }
            _orders.Save(order);
            return "cancelled " + order.AxSalesId;
        }

        private string Reserve(AxCommand command, AxCallContext context)
        {
            var order = _orders.Get(command.OrderId.Value);
            if (string.IsNullOrEmpty(order.AxSalesId)) throw new InvalidOperationException("The order has no AX sales id yet.");
            var results = _sales.Reserve(order.AxSalesId, context);
            foreach (var r in results)
            {
                var line = order.Lines.FirstOrDefault(l => l.AxInventTransId == r.InventTransId);
                if (line == null) continue;
                line.ReservedQuantity = r.Reserved;
                line.Reservation = r.Reserved >= r.Requested ? ReservationStatus.Reserved : r.Reserved > 0 ? ReservationStatus.PartiallyReserved : ReservationStatus.NotAvailable;
                if (r.ExpectedDate.HasValue) line.ConfirmedShipDate = r.ExpectedDate;
            }
            _orders.Save(order);
            return $"reserved {results.Sum(r => r.Reserved):0.##} of {results.Sum(r => r.Requested):0.##}";
        }

        private string Release(AxCommand command, AxCallContext context)
        {
            var order = _orders.Get(command.OrderId.Value);
            _sales.ReleaseReservation(order.AxSalesId, context);
            foreach (var line in order.Lines) { line.Reservation = ReservationStatus.Released; line.ReservedQuantity = 0; }
            _orders.Save(order);
            return "released " + order.AxSalesId;
        }

        private string ReadOrder(AxCommand command, AxCallContext context)
        {
            var order = _orders.Get(command.OrderId.Value);
            if (string.IsNullOrEmpty(order.AxSalesId)) return "no AX order";
            var snapshot = _reader.ReadSalesOrder(order.AxSalesId, context);
            var changes = _statusSync.Apply(order, snapshot);
            return order.AxSalesId + " " + snapshot.SalesStatus + "/" + snapshot.DocumentStatus + (changes.Count == 0 ? "" : ": " + string.Join(", ", changes));
        }

        private void EnsureDealer(Dealer dealer, AxCallContext context)
        {
            if (dealer.LastSyncedUtc.HasValue && dealer.Addresses.All(a => !string.IsNullOrEmpty(a.AxLocationId))) return;
            var result = _sales.EnsureCustomer(AxDocumentMapper.Customer(dealer), context);
            foreach (var address in dealer.Addresses)
                if (result.LocationIdsByExternalCode.TryGetValue("ADDR-" + address.Id, out var locationId)) address.AxLocationId = locationId;
            dealer.LastSyncedUtc = _clock.UtcNow;
            _dealers.Save(dealer);
        }

        private void EnsureConfigurations(AxSalesDocument document, AxCallContext context)
        {
            foreach (var line in document.Lines.Where(l => !string.IsNullOrEmpty(l.ConfigId)).GroupBy(l => l.ConfigId).Select(g => g.First()))
                _sales.EnsureConfiguration(line.ItemId, line.ConfigId, line.OptionItemIds, context);
        }

        private void MarkError(AxCommand command, string message)
        {
            if (!command.OrderId.HasValue) return;
            var order = _orders.Get(command.OrderId.Value);
            if (order == null || !OrderStatusTransitions.CanMove(order.Status, OrderStatus.Error)) return;
            order.ErrorMessage = message.Length > 2000 ? message.Substring(0, 2000) : message;
            order.ChangeStatus(OrderStatus.Error, "ax-connector", command.CommandType + " failed", _clock.UtcNow);
            _orders.Save(order);
            _notifications.OrderError(order, _dealers.Get(order.DealerId), command, message);
        }
    }
}
