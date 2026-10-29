using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailspin.DealerOrders.Domain
{
    public enum ProductFamily
    {
        Harvester = 0,
        Forwarder = 1,
        CompactLoader = 2,
        Attachment = 3,
        Parts = 4
    }

    public enum OptionGroupKind
    {
        /// <summary>Exactly one option must be selected.</summary>
        SingleRequired = 0,
        /// <summary>At most one option may be selected.</summary>
        SingleOptional = 1,
        /// <summary>Any number of options.</summary>
        Multiple = 2
    }

    public enum OptionRuleKind
    {
        Requires = 0,
        Excludes = 1,
        Includes = 2,
        Condition = 3
    }

    /// <summary>A configurable machine model (AX product master with configuration dimension).</summary>
    public class ProductModel
    {
        public ProductModel()
        {
            OptionGroups = new List<OptionGroup>();
            Rules = new List<OptionRule>();
        }

        public string ModelCode { get; set; }

        /// <summary>AX product master item id.</summary>
        public string ItemId { get; set; }

        public string Name { get; set; }
        public ProductFamily Family { get; set; }
        public int ModelYear { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public int LeadTimeWeeks { get; set; }
        public string ImageUrl { get; set; }
        public string ExportControlClass { get; set; }

        /// <summary>Markets (ISO country codes) where the model can be sold. Empty means all.</summary>
        public List<string> Markets { get; set; }

        public List<OptionGroup> OptionGroups { get; set; }
        public List<OptionRule> Rules { get; set; }

        public IEnumerable<ProductOption> AllOptions => OptionGroups.SelectMany(g => g.Options);

        public ProductOption FindOption(string code) =>
            AllOptions.FirstOrDefault(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase));

        public OptionGroup GroupOf(string optionCode) =>
            OptionGroups.FirstOrDefault(g => g.Options.Any(o => string.Equals(o.Code, optionCode, StringComparison.OrdinalIgnoreCase)));

        public bool IsAvailableIn(string countryCode) =>
            Markets == null || Markets.Count == 0 || Markets.Contains(countryCode, StringComparer.OrdinalIgnoreCase);

        public bool IsValidOn(DateTime date) => date >= ValidFrom && (!ValidTo.HasValue || date <= ValidTo.Value);
    }

    public class OptionGroup
    {
        public OptionGroup()
        {
            Options = new List<ProductOption>();
        }

        public string Code { get; set; }
        public string Name { get; set; }
        public OptionGroupKind Kind { get; set; }
        public int SortOrder { get; set; }
        public List<ProductOption> Options { get; set; }
    }

    public class ProductOption
    {
        public string Code { get; set; }
        public string Name { get; set; }

        /// <summary>AX item id of the option (BOM component); empty for options that are only a configuration attribute.</summary>
        public string ItemId { get; set; }

        public bool IsDefault { get; set; }

        /// <summary>Markets where the option is offered. Empty means all markets of the model.</summary>
        public List<string> Markets { get; set; }

        /// <summary>Option adds weeks to the standard lead time (special paint, extended crane...).</summary>
        public int ExtraLeadTimeWeeks { get; set; }

        /// <summary>Discount group: options can carry a different dealer discount than the base machine.</summary>
        public string DiscountGroup { get; set; }

        public bool IsAvailableIn(string countryCode) =>
            Markets == null || Markets.Count == 0 || Markets.Contains(countryCode, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A configuration rule. Requires/Excludes/Includes relate one option to others; Condition rules carry an
    /// expression such as <c>CRANE-11M and CAB-LEVEL =&gt; FIRE-SUPP</c> evaluated by the configurator.
    /// </summary>
    public class OptionRule
    {
        public string Id { get; set; }
        public OptionRuleKind Kind { get; set; }
        public string Option { get; set; }
        public List<string> Targets { get; set; }
        public string Expression { get; set; }
        public string Message { get; set; }
    }
}
