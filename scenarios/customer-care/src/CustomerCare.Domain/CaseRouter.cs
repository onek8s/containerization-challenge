using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.CustomerCare.Domain
{
    /// <summary>
    /// Picks the queue for a new case. Rules are evaluated by sequence; empty rule fields match anything.
    /// When no rule matches, the queue configured for the category is used, then the default queue.
    /// </summary>
    public class CaseRouter
    {
        private readonly IList<RoutingRule> _rules;
        private readonly IDictionary<string, Category> _categories;
        private readonly string _defaultQueue;

        public CaseRouter(IEnumerable<RoutingRule> rules, IEnumerable<Category> categories, string defaultQueue)
        {
            if (string.IsNullOrWhiteSpace(defaultQueue)) throw new ArgumentException("Default queue is required.", nameof(defaultQueue));

            _rules = (rules ?? Enumerable.Empty<RoutingRule>()).OrderBy(r => r.Sequence).ThenBy(r => r.Id).ToList();
            _categories = (categories ?? Enumerable.Empty<Category>()).ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
            _defaultQueue = defaultQueue;
        }

        public string Route(string categoryCode, Customer customer)
        {
            var postal = customer != null ? (customer.PostalCode ?? string.Empty).Trim() : string.Empty;
            var segment = customer != null ? customer.Segment : null;

            foreach (var r in _rules)
            {
                if (!Matches(r.CategoryCode, categoryCode)) continue;
                if (!Matches(r.Segment, segment)) continue;
                if (!string.IsNullOrEmpty(r.PostalCodePrefix) && !postal.StartsWith(r.PostalCodePrefix, StringComparison.Ordinal)) continue;
                return r.QueueCode;
            }

            Category category;
            if (categoryCode != null && _categories.TryGetValue(categoryCode, out category) && !string.IsNullOrEmpty(category.QueueCode))
            {
                return category.QueueCode;
            }

            return _defaultQueue;
        }

        /// <summary>Priority customers never start below High.</summary>
        public static CasePriority InitialPriority(CasePriority requested, Customer customer)
        {
            if (customer != null && customer.IsPriorityCustomer && requested < CasePriority.High) return CasePriority.High;
            return requested;
        }

        private static bool Matches(string ruleValue, string actual)
        {
            return string.IsNullOrEmpty(ruleValue) || string.Equals(ruleValue, actual, StringComparison.OrdinalIgnoreCase);
        }
    }
}
