using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using VanArsdel.Contracts.Pos;
using VanArsdel.StoreOps.Api.Data;
using VanArsdel.StoreOps.Api.Domain;
using VanArsdel.StoreOps.Api.Services;

namespace VanArsdel.StoreOps.Api.Consumers
{
    /// <summary>Keeps the daily takings per store up to date from the POS files.</summary>
    public class StoreSalesConsumer : IConsumer<StoreSalesReceived>
    {
        private readonly StoreOpsDbContext _db;
        private readonly StoreNotifier _notifier;

        public StoreSalesConsumer(StoreOpsDbContext db, StoreNotifier notifier)
        {
            _db = db;
            _notifier = notifier;
        }

        public async Task Consume(ConsumeContext<StoreSalesReceived> context)
        {
            var m = context.Message;
            var date = m.BusinessDate.Date;
            var takings = await _db.Takings.SingleOrDefaultAsync(t => t.StoreCode == m.StoreCode && t.BusinessDate == date);
            if (takings == null)
            {
                takings = new DailyTakings { StoreCode = m.StoreCode, BusinessDate = date };
                _db.Takings.Add(takings);
            }
            else if (string.Equals(takings.LastFile, m.FileName, StringComparison.OrdinalIgnoreCase))
            {
                // Same file delivered twice (the POS re-sends after a network break).
                return;
            }

            foreach (var receipt in m.Receipts)
            {
                if (receipt.IsReturn)
                {
                    takings.ReturnsValue += Math.Abs(receipt.Total);
                }
                else
                {
                    takings.Receipts++;
                    takings.Items += receipt.Lines.Sum(l => l.Quantity);
                    takings.NetSales += receipt.Total - receipt.VatTotal;
                }
                if (!string.IsNullOrEmpty(receipt.LoyaltyCardNo))
                {
                    takings.LoyaltyReceipts++;
                }
                foreach (var p in receipt.Payments)
                {
                    switch ((p.Tender ?? string.Empty).ToUpperInvariant())
                    {
                        case "CASH":
                            takings.CashTotal += p.Amount;
                            break;
                        case "CARD":
                            takings.CardTotal += p.Amount;
                            break;
                        default:
                            takings.OtherTotal += p.Amount;
                            break;
                    }
                }
            }
            takings.LastFile = m.FileName;
            takings.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _notifier.SendAsync(m.StoreCode, StoreNotifier.SalesUpdated, new
            {
                takings.StoreCode, takings.BusinessDate, takings.Receipts, takings.Items, takings.NetSales, takings.ReturnsValue,
            });
        }
    }
}
