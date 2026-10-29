using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using VanArsdel.Contracts.Orders;
using VanArsdel.Contracts.Pos;
using VanArsdel.Contracts.Returns;
using VanArsdel.Customers.Api.Documents;
using VanArsdel.Customers.Api.Domain;

namespace VanArsdel.Customers.Api.Consumers
{
    /// <summary>Points from store receipts (card scanned at the till), web orders and returns.</summary>
    public class LoyaltyConsumers : IConsumer<StoreSalesReceived>, IConsumer<OrderCompleted>, IConsumer<ReturnCompleted>
    {
        private readonly ICustomerStore _store;
        private readonly CustomerService _customers;
        private readonly ILogger<LoyaltyConsumers> _logger;

        public LoyaltyConsumers(ICustomerStore store, CustomerService customers, ILogger<LoyaltyConsumers> logger)
        {
            _store = store;
            _customers = customers;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<StoreSalesReceived> context)
        {
            var m = context.Message;
            var receipts = m.Receipts.Where(r => !string.IsNullOrEmpty(r.LoyaltyCardNo)).ToList();
            foreach (var r in receipts)
            {
                var type = r.IsReturn ? "Return" : "Earn";
                var amount = LoyaltyRules.ToEur(Math.Abs(r.Total), m.Currency);
                var updated = await _customers.UpdateAsync(ct => _store.FindByCardAsync(r.LoyaltyCardNo, ct),
                    c => c.Loyalty != null && LoyaltyRules.Apply(c.Loyalty, type, amount, "POS:" + m.StoreCode, $"{m.StoreCode}/{r.ReceiptNo}", r.Timestamp) != 0,
                    context.CancellationToken);
                if (updated == null) _logger.LogInformation("Unknown loyalty card {CardNo} on receipt {Store}/{Receipt}", r.LoyaltyCardNo, m.StoreCode, r.ReceiptNo);
            }
        }

        public async Task Consume(ConsumeContext<OrderCompleted> context)
        {
            var m = context.Message;
            var amount = LoyaltyRules.ToEur(m.Total, m.Currency);
            await _customers.UpdateAsync(ct => string.IsNullOrEmpty(m.LoyaltyCardNo) ? _store.FindByEmailAsync(m.CustomerEmail, ct) : _store.FindByCardAsync(m.LoyaltyCardNo, ct),
                c => c.Loyalty != null && LoyaltyRules.Apply(c.Loyalty, "Earn", amount, m.Channel, m.OrderNo, m.CompletedUtc) != 0,
                context.CancellationToken);
        }

        public async Task Consume(ConsumeContext<ReturnCompleted> context)
        {
            var m = context.Message;
            if (string.IsNullOrEmpty(m.LoyaltyCardNo) && string.IsNullOrEmpty(m.CustomerEmail)) return;
            var amount = LoyaltyRules.ToEur(m.RefundAmount, m.Currency);
            await _customers.UpdateAsync(ct => string.IsNullOrEmpty(m.LoyaltyCardNo) ? _store.FindByEmailAsync(m.CustomerEmail, ct) : _store.FindByCardAsync(m.LoyaltyCardNo, ct),
                c => c.Loyalty != null && LoyaltyRules.Apply(c.Loyalty, "Return", amount, "Returns", m.RmaNo, m.CompletedUtc) != 0,
                context.CancellationToken);
        }
    }
}
