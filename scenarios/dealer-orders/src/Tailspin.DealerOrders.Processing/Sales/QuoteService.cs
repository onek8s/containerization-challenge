using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Contracts.Portal;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Notifications;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Sales
{
    public class QuoteService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(QuoteService));

        private readonly IQuoteStore _quotes;
        private readonly IDealerStore _dealers;
        private readonly ICatalog _catalog;
        private readonly IApprovalStore _approvals;
        private readonly ConfigurationEngine _engine;
        private readonly PriceCalculator _prices;
        private readonly LeadTimeCalculator _leadTimes;
        private readonly AxCommandFactory _commands;
        private readonly NotificationComposer _notifications;
        private readonly IClock _clock;
        private readonly ProcessingSettings _settings;

        public QuoteService(IQuoteStore quotes, IDealerStore dealers, ICatalog catalog, IApprovalStore approvals, ConfigurationEngine engine,
            PriceCalculator prices, LeadTimeCalculator leadTimes, AxCommandFactory commands, NotificationComposer notifications, IClock clock,
            ProcessingSettings settings)
        {
            _quotes = quotes;
            _dealers = dealers;
            _catalog = catalog;
            _approvals = approvals;
            _engine = engine;
            _prices = prices;
            _leadTimes = leadTimes;
            _commands = commands;
            _notifications = notifications;
            _clock = clock;
            _settings = settings;
        }

        /// <summary>Validates and prices a configuration for the configurator page (no data is stored).</summary>
        public ConfigurationResultDto Evaluate(Dealer dealer, ConfigurationRequest request)
        {
            var model = _catalog.Get(request.ModelCode) ?? throw new KeyNotFoundException($"Model {request.ModelCode} not found.");
            var market = string.IsNullOrEmpty(request.DeliveryCountry) ? dealer.CountryCode : request.DeliveryCountry;
            var outcome = _engine.Resolve(model, request.Options, market, _clock.Today);
            var result = new ConfigurationResultDto
            {
                IsValid = outcome.IsValid,
                ConfigurationId = outcome.Configuration.ConfigurationId,
                Options = outcome.Configuration.Options,
                Errors = outcome.Errors,
                Warnings = outcome.Warnings,
                AddedByRules = outcome.Added,
                Currency = dealer.Currency
            };

            var price = _prices.Calculate(model, outcome.Configuration, dealer, request.Quantity <= 0 ? 1 : request.Quantity, request.ExtraDiscount, market, _clock.Today);
            result.PriceLines = price.Lines.Select(l => new PriceLineDto
            {
                Code = l.Code,
                Description = l.Description,
                ListPrice = l.ListPrice,
                DiscountPercent = l.DiscountPercent,
                NetPrice = l.NetPrice,
                Note = l.Note
            }).ToList();
            result.ListTotal = price.ListTotal;
            result.DiscountTotal = price.DiscountTotal;
            result.NetTotal = price.NetTotal;
            result.Freight = price.Freight;
            result.Warnings.AddRange(price.Problems);
            result.LeadTimeWeeks = _leadTimes.LeadTimeWeeks(model, outcome.Configuration);
            result.EarliestDelivery = _leadTimes.EarliestDelivery(_clock.Today, result.LeadTimeWeeks);
            result.NeedsApproval = request.ExtraDiscount > dealer.MaxSelfApprovedDiscount;
            return result;
        }

        public Quote CreateFromConfiguration(int dealerId, string user, ConfigurationRequest request, string endCustomer, string endCustomerCountry, string reference)
        {
            var dealer = _dealers.Get(dealerId);
            if (!dealer.CanOrder) throw new SalesRuleException($"Dealer {dealer.AccountNumber} is {dealer.Status} and cannot create quotes.");
            var evaluation = Evaluate(dealer, request);
            if (!evaluation.IsValid) throw new SalesRuleException("The configuration is not valid: " + string.Join(" ", evaluation.Errors));

            var today = _clock.Today;
            var quote = new Quote
            {
                QuoteNumber = DocumentNumbers.Quote(today.Year, _quotes.NextNumber(today.Year)),
                DealerId = dealer.Id,
                CreatedBy = user,
                CreatedUtc = _clock.UtcNow,
                ValidUntil = today.AddDays(_settings.QuoteValidityDays),
                Status = QuoteStatus.Draft,
                Currency = dealer.Currency,
                EndCustomerName = endCustomer,
                EndCustomerCountry = endCustomerCountry ?? dealer.CountryCode,
                Reference = reference,
                RequestedExtraDiscount = request.ExtraDiscount
            };
            quote.Lines.Add(LineFrom(1, request, evaluation));
            _quotes.Add(quote);
            Log.InfoFormat("Quote {0} created by {1} for {2}: {3}", quote.QuoteNumber, user, dealer.AccountNumber, evaluation.ConfigurationId);
            return quote;
        }

        public void AddConfiguration(int quoteId, ConfigurationRequest request)
        {
            var quote = Editable(quoteId);
            var dealer = _dealers.Get(quote.DealerId);
            var evaluation = Evaluate(dealer, request);
            if (!evaluation.IsValid) throw new SalesRuleException("The configuration is not valid: " + string.Join(" ", evaluation.Errors));
            quote.Lines.Add(LineFrom(quote.Lines.Count == 0 ? 1 : quote.Lines.Max(l => l.LineNumber) + 1, request, evaluation));
            _quotes.Save(quote);
        }

        public void AddPart(int quoteId, string itemId, decimal quantity)
        {
            var quote = Editable(quoteId);
            var dealer = _dealers.Get(quote.DealerId);
            var description = _catalog.PartDescription(itemId) ?? throw new SalesRuleException($"Item {itemId} is not sold to dealers.");
            var price = _prices.PricePart(itemId, description, dealer, quantity, _clock.Today);
            if (price.Note == "no price" || price.ListPrice == 0) throw new SalesRuleException($"No price for {itemId} in {dealer.Currency}.");
            quote.Lines.Add(new QuoteLine
            {
                LineNumber = quote.Lines.Count == 0 ? 1 : quote.Lines.Max(l => l.LineNumber) + 1,
                ItemId = itemId,
                Description = description,
                Quantity = quantity,
                ListPrice = price.ListPrice,
                DiscountPercent = price.DiscountPercent,
                NetPrice = price.NetPrice
            });
            _quotes.Save(quote);
        }

        public void RemoveLine(int quoteId, int lineNumber)
        {
            var quote = Editable(quoteId);
            var line = quote.Lines.FirstOrDefault(l => l.LineNumber == lineNumber) ?? throw new SalesRuleException($"Line {lineNumber} not found.");
            quote.Lines.Remove(line);
            _quotes.Save(quote);
        }

        public void Submit(int quoteId, string user, decimal extraDiscount)
        {
            var quote = Editable(quoteId);
            var dealer = _dealers.Get(quote.DealerId);
            if (quote.Lines.Count == 0) throw new SalesRuleException("The quote has no lines.");
            if (quote.IsExpired(_clock.Today)) throw new SalesRuleException("The quote has expired. Create a new quote.");

            quote.RequestedExtraDiscount = extraDiscount;
            quote.Status = QuoteStatus.Submitted;

            if (extraDiscount > dealer.MaxSelfApprovedDiscount)
            {
                quote.Status = QuoteStatus.PendingApproval;
                var request = new ApprovalRequest
                {
                    QuoteId = quote.Id,
                    DealerId = dealer.Id,
                    Reason = ApprovalReason.DiscountAboveLimit,
                    Details = $"Extra discount {extraDiscount:0.##} % requested, dealer limit {dealer.MaxSelfApprovedDiscount:0.##} %. End customer: {quote.EndCustomerName}.",
                    Amount = quote.TotalNet,
                    Currency = quote.Currency,
                    RequestedDiscount = extraDiscount,
                    RequestedUtc = _clock.UtcNow,
                    RequestedBy = user,
                    Decision = ApprovalDecision.Pending
                };
                _approvals.Add(request);
                _quotes.Save(quote);
                _notifications.ApprovalNeeded(request, dealer, quote.QuoteNumber);
                Log.InfoFormat("Quote {0} needs approval ({1} %)", quote.QuoteNumber, extraDiscount);
                return;
            }

            Approve(quote, extraDiscount, null, user);
        }

        internal void Approve(Quote quote, decimal discount, string comment, string user)
        {
            quote.Status = QuoteStatus.Approved;
            quote.ApprovedExtraDiscount = discount;
            quote.ApprovalComment = comment;
            _quotes.Save(quote);
            _commands.ForQuote(Domain.AxCommandType.RegisterQuote, quote, user);
        }

        internal void Reject(Quote quote, string comment)
        {
            quote.Status = QuoteStatus.Rejected;
            quote.ApprovalComment = comment;
            _quotes.Save(quote);
        }

        public void Cancel(int quoteId, string user)
        {
            var quote = _quotes.Get(quoteId);
            if (quote.Status == QuoteStatus.Converted || quote.Status == QuoteStatus.Accepted)
                throw new SalesRuleException("An accepted quote cannot be cancelled; cancel the order instead.");
            quote.Status = QuoteStatus.Cancelled;
            _quotes.Save(quote);
            Log.InfoFormat("Quote {0} cancelled by {1}", quote.QuoteNumber, user);
        }

        /// <summary>Marks open quotes past their validity as expired. Returns the number expired.</summary>
        public int ExpireQuotes()
        {
            var count = 0;
            foreach (var quote in _quotes.Open().Where(q => q.IsExpired(_clock.Today)))
            {
                quote.Status = QuoteStatus.Expired;
                _quotes.Save(quote);
                count++;
            }
            return count;
        }

        private Quote Editable(int quoteId)
        {
            var quote = _quotes.Get(quoteId) ?? throw new KeyNotFoundException($"Quote {quoteId} not found.");
            if (!quote.CanEdit) throw new SalesRuleException($"Quote {quote.QuoteNumber} is {quote.Status} and cannot be changed.");
            return quote;
        }

        private QuoteLine LineFrom(int lineNumber, ConfigurationRequest request, ConfigurationResultDto evaluation)
        {
            var model = _catalog.Get(request.ModelCode);
            var quantity = request.Quantity <= 0 ? 1 : request.Quantity;
            var list = evaluation.PriceLines.Sum(p => p.ListPrice);
            var net = evaluation.PriceLines.Sum(p => p.NetPrice);
            return new QuoteLine
            {
                LineNumber = lineNumber,
                ItemId = model.ItemId,
                ModelCode = model.ModelCode,
                ConfigurationId = evaluation.ConfigurationId,
                OptionCodes = string.Join(",", evaluation.Options),
                Description = model.Name,
                Quantity = quantity,
                ListPrice = list,
                NetPrice = net,
                DiscountPercent = list == 0 ? 0 : Math.Round((list - net) / list * 100m, 4),
                LeadTimeWeeks = evaluation.LeadTimeWeeks
            };
        }
    }

    [Serializable]
    public class SalesRuleException : Exception
    {
        public SalesRuleException(string message) : base(message) { }
        protected SalesRuleException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
