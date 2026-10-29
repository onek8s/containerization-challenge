using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Litware.OrderHub.Contracts;

namespace Litware.OrderHub.Core.Channels
{
    /// <summary>
    /// Order export of the marketplace (Trailmarket seller centre): semicolon separated UTF-8,
    /// one row per order line, amounts with a decimal comma.
    /// </summary>
    public static class MarketplaceOrderFile
    {
        private static readonly string[] RequiredColumns =
        {
            "OrderRef", "OrderDate", "BuyerName", "BuyerEmail", "ShipName", "ShipStreet",
            "ShipPostalCode", "ShipCity", "ShipCountry", "Sku", "Title", "Qty", "UnitPrice", "Currency", "ShippingFee"
        };

        private static readonly CultureInfo Amounts = CultureInfo.GetCultureInfo("de-DE");

        public static IReadOnlyList<MarketplaceOrder> Read(Stream stream)
        {
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true))
            {
                return Read(reader);
            }
        }

        public static IReadOnlyList<MarketplaceOrder> Read(TextReader reader)
        {
            string header = reader.ReadLine();
            if (header == null) return new MarketplaceOrder[0];

            var columns = CsvLine.Split(header, ';').Select(c => c.Trim()).ToList();
            foreach (var required in RequiredColumns)
            {
                if (!columns.Contains(required))
                {
                    throw new FormatException("Marketplace file is missing column " + required + ".");
                }
            }

            var orders = new List<MarketplaceOrder>();
            var byRef = new Dictionary<string, MarketplaceOrder>(StringComparer.Ordinal);
            string line;
            int lineNumber = 1;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (line.Trim().Length == 0) continue;

                var fields = CsvLine.Split(line, ';');
                if (fields.Count < columns.Count)
                {
                    throw new FormatException("Line " + lineNumber + " has " + fields.Count + " fields, expected " + columns.Count + ".");
                }

                Func<string, string> get = name => fields[columns.IndexOf(name)].Trim();
                string orderRef = get("OrderRef");

                MarketplaceOrder order;
                if (!byRef.TryGetValue(orderRef, out order))
                {
                    order = new MarketplaceOrder
                    {
                        OrderRef = orderRef,
                        OrderDate = DateTimeOffset.Parse(get("OrderDate"), CultureInfo.InvariantCulture),
                        Document = new OrderDocument
                        {
                            CustomerName = get("BuyerName"),
                            CustomerEmail = get("BuyerEmail"),
                            Currency = get("Currency"),
                            ShippingAmount = ParseAmount(get("ShippingFee"), lineNumber),
                            ShippingMethod = "standard",
                            CustomerReference = orderRef,
                            ShipTo = new Address
                            {
                                Name = get("ShipName"),
                                Street = get("ShipStreet"),
                                PostalCode = get("ShipPostalCode"),
                                City = get("ShipCity"),
                                CountryCode = get("ShipCountry").ToUpperInvariant()
                            }
                        }
                    };
                    byRef.Add(orderRef, order);
                    orders.Add(order);
                }

                int qty;
                if (!int.TryParse(get("Qty"), NumberStyles.Integer, CultureInfo.InvariantCulture, out qty))
                {
                    throw new FormatException("Line " + lineNumber + ": quantity '" + get("Qty") + "' is not a number.");
                }

                order.Document.Lines.Add(new OrderLineDocument
                {
                    Sku = get("Sku"),
                    Description = get("Title"),
                    Quantity = qty,
                    UnitPrice = ParseAmount(get("UnitPrice"), lineNumber)
                });
            }

            return orders;
        }

        private static decimal ParseAmount(string value, int lineNumber)
        {
            decimal amount;
            if (!decimal.TryParse(value, NumberStyles.Number, Amounts, out amount))
            {
                throw new FormatException("Line " + lineNumber + ": amount '" + value + "' is not valid.");
            }
            return amount;
        }
    }

    public class MarketplaceOrder
    {
        public string OrderRef { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public OrderDocument Document { get; set; }
    }
}
