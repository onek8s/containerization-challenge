using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Contracts.Portal;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.DealerPortal.Models;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sales;

namespace Tailspin.DealerOrders.DealerPortal.Controllers
{
    [PortalRole(PortalRoles.DealerSales, PortalRoles.DealerAdmin)]
    public class ConfiguratorController : PortalController
    {
        private readonly ICatalog _catalog;
        private readonly IDealerStore _dealers;
        private readonly PriceCalculator _prices;
        private readonly QuoteService _quotes;
        private readonly IQuoteStore _quoteStore;
        private readonly IClock _clock;

        public ConfiguratorController(ICatalog catalog, IDealerStore dealers, PriceCalculator prices, QuoteService quotes, IQuoteStore quoteStore, IClock clock)
        {
            _catalog = catalog;
            _dealers = dealers;
            _prices = prices;
            _quotes = quotes;
            _quoteStore = quoteStore;
            _clock = clock;
        }

        public ActionResult Index()
        {
            var dealer = _dealers.Get(DealerId);
            var today = _clock.Today;
            var models = _catalog.All()
                .Where(m => m.IsValidOn(today) && (m.Markets == null || m.Markets.Count == 0 || m.Markets.Contains(dealer.CountryCode)))
                .OrderBy(m => m.Family).ThenBy(m => m.ModelCode)
                .Select(m => new ModelSummaryDto
                {
                    ModelCode = m.ModelCode,
                    Name = m.Name,
                    Family = m.Family.ToString(),
                    ModelYear = m.ModelYear,
                    BasePrice = _prices.PricePart(m.ItemId, m.Name, dealer, 1, today).ListPrice,
                    Currency = dealer.Currency,
                    LeadTimeWeeks = m.LeadTimeWeeks,
                    ImageUrl = m.ImageUrl
                }).ToList();
            return View(models);
        }

        public ActionResult Configure(string id, int? quoteId)
        {
            var model = _catalog.Get(id);
            if (model == null) return HttpNotFound();
            ViewBag.ModelCode = model.ModelCode;
            ViewBag.ModelName = model.Name;
            ViewBag.QuoteId = quoteId;
            return View();
        }

        /// <summary>Option groups with the dealer's list prices, loaded by the Knockout page.</summary>
        [HttpGet]
        public ActionResult Model(string id)
        {
            var model = _catalog.Get(id);
            if (model == null) return HttpNotFound();
            var dealer = _dealers.Get(DealerId);
            var today = _clock.Today;
            var dto = new ConfiguratorModelDto
            {
                ModelCode = model.ModelCode,
                Name = model.Name,
                Currency = dealer.Currency,
                BasePrice = _prices.PricePart(model.ItemId, model.Name, dealer, 1, today).ListPrice
            };
            foreach (var group in model.OptionGroups.OrderBy(g => g.SortOrder))
            {
                var g = new OptionGroupDto { Code = group.Code, Name = group.Name, Kind = group.Kind.ToString() };
                foreach (var option in group.Options.Where(o => o.Markets == null || o.Markets.Count == 0 || o.Markets.Contains(dealer.CountryCode)))
                {
                    g.Options.Add(new OptionDto
                    {
                        Code = option.Code,
                        Name = option.Name,
                        IsDefault = option.IsDefault,
                        Price = string.IsNullOrEmpty(option.ItemId) ? 0m : _prices.PricePart(option.ItemId, option.Name, dealer, 1, today).ListPrice
                    });
                    if (option.IsDefault) dto.DefaultOptions.Add(option.Code);
                }
                dto.Groups.Add(g);
            }
            return JsonNet(dto);
        }

        [HttpPost]
        public ActionResult Evaluate()
        {
            var request = FromJsonBody<ConfigurationRequest>();
            return JsonNet(_quotes.Evaluate(_dealers.Get(DealerId), request));
        }

        /// <summary>Creates a quote from the configuration, or adds it to an open quote.</summary>
        [HttpPost, ValidateJsonAntiForgeryToken]
        public ActionResult Save()
        {
            var input = FromJsonBody<NewQuoteModel>();
            var request = new ConfigurationRequest
            {
                ModelCode = input.ModelCode,
                Options = input.Options ?? new List<string>(),
                Quantity = input.Quantity <= 0 ? 1 : input.Quantity,
                ExtraDiscount = input.ExtraDiscount,
                DeliveryCountry = input.DeliveryCountry
            };

            int quoteId;
            if (input.AddToQuoteId.HasValue)
            {
                var existing = _quoteStore.Get(input.AddToQuoteId.Value);
                if (existing == null || existing.DealerId != DealerId) return HttpNotFound();
                _quotes.AddConfiguration(existing.Id, request);
                quoteId = existing.Id;
            }
            else
            {
                quoteId = _quotes.CreateFromConfiguration(DealerId, UserName, request, input.EndCustomer, input.EndCustomerCountry, input.Reference).Id;
            }
            return JsonNet(new { quoteId, url = Url.Action("Details", "Quotes", new { id = quoteId }) });
        }
    }
}
