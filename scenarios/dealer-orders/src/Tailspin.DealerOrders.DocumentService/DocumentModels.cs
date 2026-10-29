using System;
using System.Collections.Generic;
using System.Linq;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Documents.Word;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.DocumentService
{
    /// <summary>Turns quotes, orders and dealers into the models the Word templates are filled from.</summary>
    public class DocumentModels
    {
        private static readonly Dictionary<DiscountTier, decimal> AnnualTargets = new Dictionary<DiscountTier, decimal>
        {
            [DiscountTier.A] = 2500000m,
            [DiscountTier.B] = 1200000m,
            [DiscountTier.C] = 500000m,
            [DiscountTier.Sub] = 0m
        };

        private readonly ICatalog _catalog;
        private readonly IPriceSource _prices;
        private readonly LeadTimeCalculator _leadTimes;
        private readonly IClock _clock;

        public DocumentModels(ICatalog catalog, IPriceSource prices, LeadTimeCalculator leadTimes, IClock clock)
        {
            _catalog = catalog;
            _prices = prices;
            _leadTimes = leadTimes;
            _clock = clock;
        }

        public SalesDocumentModel ForQuote(Quote quote, Dealer dealer)
        {
            var model = new SalesDocumentModel
            {
                Number = quote.QuoteNumber,
                Date = quote.CreatedUtc.Date,
                Dealer = dealer,
                DeliveryAddress = dealer.Addresses.FirstOrDefault(a => a.IsDefaultDelivery) ?? dealer.Addresses.FirstOrDefault(),
                Language = dealer.LanguageCode,
                Currency = quote.Currency,
                EndCustomer = quote.EndCustomerName,
                DealerReference = quote.Reference,
                AxReference = quote.AxQuotationId,
                SalesPerson = dealer.SalesResponsible,
                ValidUntil = quote.ValidUntil,
                DeliveryTerms = dealer.DeliveryTerms,
                ExtraDiscountPercent = quote.ApprovedExtraDiscount,
                Notes = quote.Notes
            };
            var maxWeeks = 0;
            foreach (var line in quote.Lines.OrderBy(l => l.LineNumber))
            {
                model.Lines.Add(Line(line.LineNumber, line.ItemId, line.ModelCode, line.Options, line.Description, line.Quantity,
                    line.ListPrice, line.DiscountPercent, line.NetPrice, null));
                maxWeeks = Math.Max(maxWeeks, line.LeadTimeWeeks);
            }
            if (maxWeeks > 0) model.EarliestDelivery = _leadTimes.EarliestDelivery(_clock.Today, maxWeeks);
            model.ExtraDiscountAmount = Math.Round(model.LinesTotal * quote.ApprovedExtraDiscount / 100m, Currencies.Decimals(quote.Currency));
            return model;
        }

        public SalesDocumentModel ForOrder(SalesOrder order, Dealer dealer)
        {
            var model = new SalesDocumentModel
            {
                Number = order.OrderNumber,
                Date = order.CreatedUtc.Date,
                Dealer = dealer,
                DeliveryAddress = order.DeliveryAddress ?? dealer.Addresses.FirstOrDefault(a => a.IsDefaultDelivery),
                Language = dealer.LanguageCode,
                Currency = order.Currency,
                EndCustomer = order.EndCustomerName,
                DealerReference = order.DealerPoNumber,
                AxReference = order.AxSalesId,
                SalesPerson = dealer.SalesResponsible,
                EarliestDelivery = order.ConfirmedDeliveryDate ?? order.RequestedDeliveryDate,
                DeliveryTerms = order.DeliveryTerms,
                ExtraDiscountPercent = order.ExtraDiscount,
                Freight = order.FreightCharge,
                Notes = order.Notes
            };
            foreach (var line in order.Lines.Where(l => !l.Cancelled).OrderBy(l => l.LineNumber))
                model.Lines.Add(Line(line.LineNumber, line.ItemId, line.ModelCode, SplitOptions(line.OptionCodes), line.Description, line.Quantity,
                    line.ListPrice, line.DiscountPercent, line.NetPrice, line.ConfirmedShipDate));
            model.ExtraDiscountAmount = Math.Round(model.LinesTotal * order.ExtraDiscount / 100m, Currencies.Decimals(order.Currency));
            return model;
        }

        public DealerContractModel ForContract(Dealer dealer, int year) => new DealerContractModel
        {
            Dealer = dealer,
            Year = year,
            Language = dealer.LanguageCode,
            Territory = dealer.SalesDistrict,
            AnnualTargetEur = AnnualTargets.TryGetValue(dealer.DiscountTier, out var target) ? target : 0m,
            Discounts = _prices.Discounts().Where(d => d.Tier == dealer.DiscountTier).OrderBy(d => d.DiscountGroup).ToList(),
            SignatoryName = Settings.Get("Documents.SignatoryName", "Kari Lehtonen"),
            SignatoryTitle = Settings.Get("Documents.SignatoryTitle", "Sales Director, Dealer Network")
        };

        private DocumentLine Line(int number, string itemId, string modelCode, IEnumerable<string> options, string description, decimal quantity,
            decimal listPrice, decimal discount, decimal netPrice, DateTime? shipDate)
        {
            var line = new DocumentLine
            {
                LineNumber = number,
                ItemId = itemId,
                Description = description,
                Quantity = quantity,
                ListPrice = listPrice,
                DiscountPercent = discount,
                NetPrice = netPrice,
                ShipDate = shipDate
            };
            var model = string.IsNullOrEmpty(modelCode) ? null : _catalog.Get(modelCode);
            if (model != null)
                foreach (var code in options)
                    line.OptionDescriptions.Add(model.FindOption(code)?.Name ?? code);
            return line;
        }

        private static IEnumerable<string> SplitOptions(string codes) =>
            (codes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim());
    }
}
