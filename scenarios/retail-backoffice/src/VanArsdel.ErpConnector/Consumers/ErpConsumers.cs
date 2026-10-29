using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using VanArsdel.Contracts;
using VanArsdel.Contracts.Catalog;
using VanArsdel.Contracts.Inventory;
using VanArsdel.Contracts.Orders;
using VanArsdel.Contracts.Pos;
using VanArsdel.Contracts.Returns;
using VanArsdel.Integration.Erp;

namespace VanArsdel.ErpConnector.Consumers
{
    /// <summary>Items to the ERP when the merchandisers publish them.</summary>
    public class ItemConsumer : IConsumer<ProductPublished>
    {
        private readonly ErpGateway _erp;

        public ItemConsumer(ErpGateway erp)
        {
            _erp = erp;
        }

        public Task Consume(ConsumeContext<ProductPublished> context)
        {
            var m = context.Message;
            var item = new ErpItem
            {
                ItemNo = _erp.ItemNo(m.StyleCode),
                Description = m.Name.Length > 50 ? m.Name.Substring(0, 50) : m.Name,
                ItemGroup = m.CategoryCode,
                VatGroup = m.VatClass ?? "STD",
                Variants = m.Skus.Select(s => new ErpItemVariant
                {
                    VariantCode = _erp.Split(s.Sku).Variant,
                    Gtin = s.Ean,
                    Colour = s.ColourCode,
                    Size = s.Size,
                    UnitCost = s.CostPrice,
                }).ToArray(),
            };
            // Re-publishing sends the item again (UpsertItem is idempotent on the ERP side).
            return _erp.PostOnceAsync("Item", item.ItemNo + "@" + m.PublishedUtc.ToString("yyyyMMddHHmmss"), c => c.UpsertItemAsync(item));
        }
    }

    /// <summary>Store sales as a daily sales journal per POS file, summed per variant.</summary>
    public class SalesJournalConsumer : IConsumer<StoreSalesReceived>
    {
        private readonly ErpGateway _erp;

        public SalesJournalConsumer(ErpGateway erp)
        {
            _erp = erp;
        }

        public Task Consume(ConsumeContext<StoreSalesReceived> context)
        {
            var m = context.Message;
            var vat = Markets.StandardVat.TryGetValue(m.StoreCode.Substring(0, 2), out var v) ? v : 0.24m;
            var lines = m.Receipts
                .SelectMany(r => r.Lines.Select(l => new { r.IsReturn, Line = l }))
                .GroupBy(x => x.Line.Sku)
                .Select(g =>
                {
                    var (itemNo, variant) = _erp.Split(g.Key);
                    return new ErpJournalLine
                    {
                        ItemNo = itemNo,
                        VariantCode = variant,
                        Quantity = g.Sum(x => x.IsReturn ? -Math.Abs(x.Line.Quantity) : x.Line.Quantity),
                        NetAmount = Math.Round(g.Sum(x => (x.IsReturn ? -Math.Abs(x.Line.LineTotal) : x.Line.LineTotal) / (1 + vat)), 2),
                        DiscountAmount = g.Sum(x => x.Line.Discount),
                    };
                })
                .ToArray();

            var journal = new ErpSalesJournal
            {
                ExternalReference = "POS-" + m.StoreCode + "-" + m.BusinessDate.ToString("yyyyMMdd") + "-" + m.Sequence.ToString("000"),
                SiteCode = m.StoreCode,
                PostingDate = m.BusinessDate.Date,
                CurrencyCode = m.Currency,
                Lines = lines,
            };
            return _erp.PostOnceAsync("SalesJournal", journal.ExternalReference, c => c.PostSalesJournalAsync(journal));
        }
    }

    /// <summary>Web orders are invoiced to the web shop customer of the market when they complete.</summary>
    public class InvoiceConsumer : IConsumer<OrderCompleted>, IConsumer<ReturnCompleted>
    {
        private readonly ErpGateway _erp;

        public InvoiceConsumer(ErpGateway erp)
        {
            _erp = erp;
        }

        public Task Consume(ConsumeContext<OrderCompleted> context)
        {
            var m = context.Message;
            if (m.Channel == "Store")
            {
                return Task.CompletedTask; // in the POS journal already
            }
            var vat = Markets.StandardVat.TryGetValue(m.Market ?? "FI", out var v) ? v : 0.24m;
            var invoice = new ErpSalesInvoice
            {
                ExternalReference = m.OrderNo,
                CustomerNo = "WEB-" + m.Market,
                InvoiceDate = m.CompletedUtc.Date,
                CurrencyCode = m.Currency,
                ShipFromSite = _erp.Settings.WebShopSite,
                Lines = m.Lines.Select(l =>
                {
                    var (itemNo, variant) = _erp.Split(l.Sku);
                    return new ErpJournalLine
                    {
                        ItemNo = itemNo, VariantCode = variant, Quantity = l.Quantity,
                        NetAmount = Math.Round((l.UnitPrice * l.Quantity - l.Discount) / (1 + vat), 2), DiscountAmount = l.Discount,
                    };
                }).ToArray(),
            };
            return _erp.PostOnceAsync("Invoice", invoice.ExternalReference, c => c.PostSalesInvoiceAsync(invoice));
        }

        /// <summary>Credit note for web returns. Store returns are negative lines in the POS journal.</summary>
        public Task Consume(ConsumeContext<ReturnCompleted> context)
        {
            var m = context.Message;
            if (string.IsNullOrEmpty(m.OrderNo) || m.RefundMethod == "POS")
            {
                return Task.CompletedTask;
            }
            var units = Math.Max(1, m.Lines.Sum(l => l.Quantity));
            var market = m.Currency == "SEK" ? "SE" : "FI";
            var credit = new ErpSalesInvoice
            {
                ExternalReference = m.RmaNo,
                CustomerNo = "WEB-" + market,
                InvoiceDate = m.CompletedUtc.Date,
                CurrencyCode = m.Currency,
                ShipFromSite = _erp.Settings.WebShopSite,
                Lines = m.Lines.Select(l =>
                {
                    var (itemNo, variant) = _erp.Split(l.Sku);
                    return new ErpJournalLine
                    {
                        ItemNo = itemNo, VariantCode = variant, Quantity = -l.Quantity,
                        NetAmount = -Math.Round(m.RefundAmount * l.Quantity / units / (1 + Markets.StandardVat[market]), 2),
                    };
                }).ToArray(),
            };
            return _erp.PostOnceAsync("CreditNote", credit.ExternalReference, c => c.PostSalesInvoiceAsync(credit));
        }
    }

    /// <summary>Approved replenishment proposals become ERP transfer orders; the DC ships them the same evening.</summary>
    public class TransferConsumer : IConsumer<ReplenishmentProposed>
    {
        private readonly ErpGateway _erp;

        public TransferConsumer(ErpGateway erp)
        {
            _erp = erp;
        }

        public async Task Consume(ConsumeContext<ReplenishmentProposed> context)
        {
            var m = context.Message;
            var reference = "REPL-" + m.ProposalId.ToString("N").Substring(0, 12).ToUpperInvariant();
            var order = new ErpTransferOrder
            {
                ExternalReference = reference,
                FromSite = m.FromLocation,
                ToSite = m.StoreCode,
                RequestedDate = m.CreatedUtc.Date.AddDays(1),
                Lines = m.Lines.Select(l =>
                {
                    var (itemNo, variant) = _erp.Split(l.Sku);
                    return new ErpTransferLine { ItemNo = itemNo, VariantCode = variant, Quantity = l.Quantity };
                }).ToArray(),
            };
            await _erp.PostOnceAsync("Transfer", reference, c => c.CreateTransferOrderAsync(order));
            await context.Publish(new TransferShipped { TransferNo = reference, FromLocation = m.FromLocation, ToLocation = m.StoreCode, Lines = m.Lines });
        }
    }
}
