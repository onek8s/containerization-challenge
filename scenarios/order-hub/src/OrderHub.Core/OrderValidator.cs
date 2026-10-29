using System;
using System.Collections.Generic;
using System.Linq;
using Litware.OrderHub.Contracts;

namespace Litware.OrderHub.Core
{
    public class OrderValidator
    {
        private readonly ISet<string> _countries;
        private readonly ISet<string> _currencies;

        public OrderValidator(IEnumerable<string> shipToCountries, IEnumerable<string> currencies)
        {
            _countries = new HashSet<string>(shipToCountries.Select(c => c.Trim().ToUpperInvariant()));
            _currencies = new HashSet<string>(currencies.Select(c => c.Trim().ToUpperInvariant()));
        }

        /// <summary>Returns the problems found, empty when the order can go to the warehouse.</summary>
        public IList<string> Validate(OrderDocument order, Func<string, Product> findProduct)
        {
            var problems = new List<string>();

            if (order.ShipTo == null)
            {
                problems.Add("No delivery address.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(order.ShipTo.Name)) problems.Add("Delivery name is missing.");
                if (string.IsNullOrWhiteSpace(order.ShipTo.Street)) problems.Add("Delivery street is missing.");
                if (string.IsNullOrWhiteSpace(order.ShipTo.PostalCode)) problems.Add("Postal code is missing.");
                if (string.IsNullOrWhiteSpace(order.ShipTo.City)) problems.Add("City is missing.");
                string country = (order.ShipTo.CountryCode ?? "").ToUpperInvariant();
                if (!_countries.Contains(country)) problems.Add("Delivery country '" + country + "' is not served.");
                if (order.ShipTo.Name != null && order.ShipTo.Name.Length > 60) problems.Add("Delivery name is longer than 60 characters.");
            }

            if (!_currencies.Contains((order.Currency ?? "").ToUpperInvariant()))
            {
                problems.Add("Currency '" + order.Currency + "' is not accepted.");
            }

            if (order.Lines == null || order.Lines.Count == 0)
            {
                problems.Add("Order has no lines.");
                return problems;
            }

            foreach (var line in order.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.Sku))
                {
                    problems.Add("Line '" + line.Description + "' has no SKU.");
                    continue;
                }
                var product = findProduct(line.Sku);
                if (product == null) problems.Add("Unknown SKU " + line.Sku + ".");
                else if (!product.Active) problems.Add("SKU " + line.Sku + " is discontinued.");
                if (line.Quantity <= 0 || line.Quantity > 999) problems.Add("SKU " + line.Sku + ": quantity " + line.Quantity + " is out of range.");
                if (line.UnitPrice < 0) problems.Add("SKU " + line.Sku + ": negative price.");
            }

            return problems;
        }
    }
}
