using System;
using System.Collections.Generic;
using System.Linq;
using Tailspin.DealerOrders.Contracts.Ax;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Processing.Ax
{
    /// <summary>Maps portal entities to the AX documents sent through the Business Connector.</summary>
    public class AxDocumentMapper
    {
        private readonly ICatalog _catalog;

        public AxDocumentMapper(ICatalog catalog)
        {
            _catalog = catalog;
        }

        public static AxCustomer Customer(Dealer dealer) => new AxCustomer
        {
            AccountNum = dealer.AccountNumber,
            Name = dealer.Name,
            CustGroup = "DLR-" + dealer.CountryCode,
            Currency = dealer.Currency,
            LanguageId = AxLanguage(dealer.LanguageCode),
            PriceGroup = dealer.PriceGroup,
            LineDiscount = "TIER-" + dealer.DiscountTier.ToString().ToUpperInvariant(),
            PaymTermId = dealer.PaymentTerms,
            DlvTerm = DlvTerm(dealer.DeliveryTerms),
            InventLocation = dealer.Warehouse,
            CreditMax = dealer.CreditLimit,
            SalesDistrictId = dealer.SalesDistrict,
            Email = dealer.Email,
            Phone = dealer.Phone,
            Blocked = dealer.Status == DealerStatus.Active ? AxCustomerBlocked.No : AxCustomerBlocked.All,
            Addresses = dealer.Addresses.Select(a => new AxAddress
            {
                LocationId = a.AxLocationId,
                Description = a.Name,
                Street = a.Street,
                ZipCode = a.PostalCode,
                City = a.City,
                State = a.State,
                CountryRegionId = AxCountry(a.CountryCode),
                IsPrimaryDelivery = a.IsDefaultDelivery,
                ExternalCode = "ADDR-" + a.Id
            }).ToList()
        };

        public AxSalesDocument Order(SalesOrder order, Dealer dealer)
        {
            var doc = Header(dealer, order.DealerPoNumber, order.OrderNumber, order.RequestedDeliveryDate, order.DeliveryAddress, order.Source.ToString().ToUpperInvariant());
            doc.EndCustomerName = order.EndCustomerName;
            doc.FreightCharge = order.FreightCharge;
            doc.Notes = order.Notes;
            doc.DlvTerm = DlvTerm(order.DeliveryTerms);
            foreach (var line in order.Lines.Where(l => !l.Cancelled).OrderBy(l => l.LineNumber))
                doc.Lines.Add(Line(line.LineNumber, line.ItemId, line.ModelCode, line.ConfigurationId, line.OptionCodes, line.Description, line.Quantity,
                    line.ListPrice, line.DiscountPercent, order.RequestedDeliveryDate, line.DealerItemNumber, line.AxInventTransId));
            return doc;
        }

        public AxSalesDocument Quotation(Quote quote, Dealer dealer)
        {
            var doc = Header(dealer, quote.Reference, quote.QuoteNumber, null, null, "PORTAL");
            doc.EndCustomerName = quote.EndCustomerName;
            doc.Notes = quote.Notes;
            var factor = (100m - quote.ApprovedExtraDiscount) / 100m;
            foreach (var line in quote.Lines.OrderBy(l => l.LineNumber))
            {
                var net = line.NetPrice * factor;
                var percent = line.ListPrice == 0 ? 0 : Math.Round((line.ListPrice - net) / line.ListPrice * 100m, 4);
                doc.Lines.Add(Line(line.LineNumber, line.ItemId, line.ModelCode, line.ConfigurationId, line.OptionCodes, line.Description, line.Quantity,
                    line.ListPrice, percent, null, null, null));
            }
            return doc;
        }

        /// <summary>Option item ids (BOM components) of a configured line, in option code order.</summary>
        public IList<string> OptionItems(string modelCode, string optionCodes)
        {
            if (string.IsNullOrEmpty(modelCode)) return new List<string>();
            var model = _catalog.Get(modelCode) ?? throw new InvalidOperationException($"Model {modelCode} is not in the catalog.");
            return (optionCodes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => model.FindOption(c.Trim()))
                .Where(o => o != null && !string.IsNullOrEmpty(o.ItemId))
                .Select(o => o.ItemId)
                .ToList();
        }

        private static AxSalesDocument Header(Dealer dealer, string customerRef, string portalReference, DateTime? delivery, DealerAddress address, string origin) =>
            new AxSalesDocument
            {
                CustAccount = dealer.AccountNumber,
                CustomerRef = customerRef,
                PortalReference = portalReference,
                CurrencyCode = dealer.Currency,
                DeliveryDate = delivery,
                DlvTerm = DlvTerm(dealer.DeliveryTerms),
                DeliveryLocationId = address?.AxLocationId,
                InventLocationId = dealer.Warehouse,
                SalesOrigin = origin
            };

        private AxSalesLineData Line(int number, string itemId, string modelCode, string configId, string optionCodes, string name, decimal qty,
            decimal listPrice, decimal percent, DateTime? delivery, string externalItem, string inventTransId) =>
            new AxSalesLineData
            {
                LineNumber = number,
                ItemId = itemId,
                ConfigId = configId,
                OptionItemIds = OptionItems(modelCode, optionCodes).ToList(),
                Name = name,
                Quantity = qty,
                SalesPrice = listPrice,
                LinePercent = percent,
                DeliveryDate = delivery,
                ExternalItemId = externalItem,
                InventTransId = inventTransId
            };

        public static string DlvTerm(DeliveryTerms terms) => terms.ToString().ToUpperInvariant();

        /// <summary>AX 2012 country/region ids are ISO 3166 alpha-3.</summary>
        public static string AxCountry(string iso2)
        {
            switch ((iso2 ?? "").ToUpperInvariant())
            {
                case "FI": return "FIN";
                case "SE": return "SWE";
                case "NO": return "NOR";
                case "DK": return "DNK";
                case "DE": return "DEU";
                case "AT": return "AUT";
                case "EE": return "EST";
                case "US": return "USA";
                case "CA": return "CAN";
                case "GB": return "GBR";
                case "FR": return "FRA";
                case "PL": return "POL";
                default: return iso2;
            }
        }

        public static string AxLanguage(string language)
        {
            switch ((language ?? "en").ToLowerInvariant())
            {
                case "fi": return "fi";
                case "sv": return "sv";
                case "de": return "de";
                case "nb": return "nb-no";
                default: return "en-us";
            }
        }
    }
}
