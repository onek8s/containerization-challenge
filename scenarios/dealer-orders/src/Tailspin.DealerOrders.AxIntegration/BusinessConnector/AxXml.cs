using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Tailspin.DealerOrders.Contracts.Ax;

namespace Tailspin.DealerOrders.AxIntegration.BusinessConnector
{
    /// <summary>
    /// XML exchanged with the TSDealerOrderService X++ class. Documents go to AX as one XML string per call (instead of
    /// many record calls over the Business Connector) and the X++ side answers with a result element:
    /// <code>&lt;result ok="1" id="SO19-004512"&gt;&lt;line num="1" inventTransId="LOT004000123"/&gt;&lt;info&gt;...&lt;/info&gt;&lt;/result&gt;</code>
    /// </summary>
    internal static class AxXml
    {
        private static string D(DateTime? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
        private static string N(decimal value) => value.ToString(CultureInfo.InvariantCulture);

        public static string Customer(AxCustomer c) => new XElement("customer",
            new XAttribute("accountNum", c.AccountNum),
            new XElement("name", c.Name),
            new XElement("custGroup", c.CustGroup),
            new XElement("currency", c.Currency),
            new XElement("languageId", c.LanguageId),
            new XElement("priceGroup", c.PriceGroup),
            new XElement("lineDisc", c.LineDiscount),
            new XElement("paymTermId", c.PaymTermId),
            new XElement("dlvTerm", c.DlvTerm),
            new XElement("inventLocation", c.InventLocation),
            new XElement("creditMax", N(c.CreditMax)),
            new XElement("salesDistrictId", c.SalesDistrictId),
            new XElement("email", c.Email),
            new XElement("phone", c.Phone),
            new XElement("blocked", (int)c.Blocked),
            new XElement("addresses", c.Addresses.Select(a => new XElement("address",
                new XAttribute("externalCode", a.ExternalCode ?? ""),
                new XAttribute("locationId", a.LocationId ?? ""),
                new XAttribute("primaryDelivery", a.IsPrimaryDelivery ? 1 : 0),
                new XElement("description", a.Description),
                new XElement("street", a.Street),
                new XElement("zipCode", a.ZipCode),
                new XElement("city", a.City),
                new XElement("state", a.State),
                new XElement("countryRegionId", a.CountryRegionId))))).ToString(SaveOptions.DisableFormatting);

        public static string SalesDocument(string root, AxSalesDocument d) => new XElement(root,
            new XAttribute("custAccount", d.CustAccount),
            new XElement("customerRef", d.CustomerRef),
            new XElement("purchOrderFormNum", d.PortalReference),
            new XElement("currencyCode", d.CurrencyCode),
            new XElement("deliveryDate", D(d.DeliveryDate)),
            new XElement("dlvTerm", d.DlvTerm),
            new XElement("deliveryLocation", d.DeliveryLocationId),
            new XElement("inventLocationId", d.InventLocationId),
            new XElement("salesOriginId", d.SalesOrigin),
            new XElement("endCustomer", d.EndCustomerName),
            new XElement("freight", N(d.FreightCharge)),
            new XElement("notes", d.Notes),
            new XElement("lines", d.Lines.Select(l => new XElement("line",
                new XAttribute("num", l.LineNumber),
                new XAttribute("inventTransId", l.InventTransId ?? ""),
                new XAttribute("delete", l.Delete ? 1 : 0),
                new XElement("itemId", l.ItemId),
                new XElement("configId", l.ConfigId),
                new XElement("name", l.Name),
                new XElement("qty", N(l.Quantity)),
                new XElement("salesPrice", N(l.SalesPrice)),
                new XElement("linePercent", N(l.LinePercent)),
                new XElement("deliveryDate", D(l.DeliveryDate)),
                new XElement("externalItemId", l.ExternalItemId))))).ToString(SaveOptions.DisableFormatting);

        public static string Configuration(string itemId, string configId, IEnumerable<string> optionItems) => new XElement("configuration",
            new XAttribute("itemId", itemId), new XAttribute("configId", configId),
            optionItems.Select(o => new XElement("option", o))).ToString(SaveOptions.DisableFormatting);

        public static XElement Result(object returned, string what)
        {
            var text = returned as string;
            if (string.IsNullOrEmpty(text)) throw new AxBusinessException($"{what} returned no result.");
            var result = XElement.Parse(text);
            var info = result.Elements("info").Select(i => (string)i).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            if ((string)result.Attribute("ok") != "1")
                throw new AxBusinessException($"{what} failed in AX.", info);
            return result;
        }

        public static AxDocumentResult DocumentResult(XElement result) => new AxDocumentResult
        {
            DocumentId = (string)result.Attribute("id"),
            InventTransIdsByLine = result.Elements("line").ToDictionary(l => (int)l.Attribute("num"), l => (string)l.Attribute("inventTransId")),
            Infolog = result.Elements("info").Select(i => (string)i).ToList()
        };
    }
}
