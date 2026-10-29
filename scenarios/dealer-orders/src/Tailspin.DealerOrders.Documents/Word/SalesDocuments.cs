using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tailspin.DealerOrders.Configurator;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Documents.Word
{
    public class DocumentLine
    {
        public int LineNumber { get; set; }
        public string ItemId { get; set; }
        public string Description { get; set; }
        public IList<string> OptionDescriptions { get; set; } = new List<string>();
        public decimal Quantity { get; set; }
        public decimal ListPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal NetPrice { get; set; }
        public decimal NetAmount => NetPrice * Quantity;
        public DateTime? ShipDate { get; set; }
    }

    public class SalesDocumentModel
    {
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public Dealer Dealer { get; set; }
        public DealerAddress DeliveryAddress { get; set; }
        public string Language { get; set; }
        public string Currency { get; set; }
        public string EndCustomer { get; set; }
        public string DealerReference { get; set; }
        public string AxReference { get; set; }
        public string SalesPerson { get; set; }
        public DateTime? ValidUntil { get; set; }
        public DateTime? EarliestDelivery { get; set; }
        public DeliveryTerms DeliveryTerms { get; set; }
        public decimal ExtraDiscountPercent { get; set; }
        public decimal ExtraDiscountAmount { get; set; }
        public decimal Freight { get; set; }
        public string Notes { get; set; }
        public List<DocumentLine> Lines { get; set; } = new List<DocumentLine>();

        public decimal LinesTotal => Lines.Sum(l => l.NetAmount);
        public decimal Total => LinesTotal - ExtraDiscountAmount + Freight;
    }

    public class DealerContractModel
    {
        public Dealer Dealer { get; set; }
        public int Year { get; set; }
        public string Language { get; set; }
        public string Territory { get; set; }
        public decimal AnnualTargetEur { get; set; }
        public IList<DiscountEntry> Discounts { get; set; } = new List<DiscountEntry>();
        public string SignatoryName { get; set; }
        public string SignatoryTitle { get; set; }
    }

    /// <summary>Builds quotes, order confirmations and dealer agreements from the Word templates.</summary>
    public class SalesDocumentBuilder
    {
        private readonly Func<string, byte[]> _templateLoader;

        /// <param name="templateLoader">Returns the template bytes for a template name such as <c>Quote.en.docx</c>.</param>
        public SalesDocumentBuilder(Func<string, byte[]> templateLoader)
        {
            _templateLoader = templateLoader;
        }

        public byte[] Quote(SalesDocumentModel model) => Build("Quote", model, DocumentTexts.Get(model.Language, "quote.title"));

        public byte[] OrderConfirmation(SalesDocumentModel model) => Build("OrderConfirmation", model, DocumentTexts.Get(model.Language, "confirmation.title"));

        public byte[] DealerContract(DealerContractModel model)
        {
            var lang = model.Language ?? "en";
            var fields = new Dictionary<string, string>
            {
                ["title"] = DocumentTexts.Get(lang, "contract.title") + " " + model.Year,
                ["dealer.name"] = model.Dealer.Name,
                ["dealer.account"] = model.Dealer.AccountNumber,
                ["dealer.address"] = model.Dealer.Addresses.FirstOrDefault()?.ToString() ?? "",
                ["territory"] = model.Territory ?? model.Dealer.SalesDistrict,
                ["tier"] = model.Dealer.DiscountTier.ToString(),
                ["period"] = $"1.1.{model.Year} - 31.12.{model.Year}",
                ["target"] = DocumentTexts.Amount(model.AnnualTargetEur, "EUR", lang),
                ["paymentterms"] = model.Dealer.PaymentTerms,
                ["creditlimit"] = DocumentTexts.Amount(model.Dealer.CreditLimit, model.Dealer.Currency, lang),
                ["deliveryterms"] = DeliveryText(model.Dealer.DeliveryTerms, lang),
                ["warranty"] = DocumentTexts.Get(lang, "warranty"),
                ["signatory.name"] = model.SignatoryName ?? "",
                ["signatory.title"] = model.SignatoryTitle ?? ""
            };
            var rows = model.Discounts.OrderBy(d => d.DiscountGroup)
                .Select(d => (IDictionary<string, string>)new Dictionary<string, string>
                {
                    ["group"] = d.DiscountGroup,
                    ["percent"] = d.Percent.ToString("0.##", DocumentTexts.Culture(lang)) + " %"
                }).ToList();
            var template = new WordTemplate(_templateLoader(TemplateName("DealerContract", lang)));
            return template.Fill(fields, new Dictionary<string, IList<IDictionary<string, string>>> { ["discount"] = rows });
        }

        private byte[] Build(string kind, SalesDocumentModel model, string title)
        {
            var lang = model.Language ?? "en";
            var culture = DocumentTexts.Culture(lang);
            var delivery = model.DeliveryAddress ?? model.Dealer.Addresses.FirstOrDefault(a => a.IsDefaultDelivery);
            var fields = new Dictionary<string, string>
            {
                ["title"] = title,
                ["number"] = model.Number,
                ["date"] = DocumentTexts.Date(model.Date, lang),
                ["dealer.name"] = model.Dealer.Name,
                ["dealer.account"] = model.Dealer.AccountNumber,
                ["dealer.reference"] = model.DealerReference ?? "",
                ["delivery.address"] = delivery?.ToString() ?? "",
                ["endcustomer"] = model.EndCustomer ?? "",
                ["axreference"] = model.AxReference ?? "",
                ["salesperson"] = model.SalesPerson ?? model.Dealer.SalesResponsible ?? "",
                ["deliveryterms"] = DeliveryText(model.DeliveryTerms, lang),
                ["paymentterms"] = model.Dealer.PaymentTerms ?? "",
                ["subtotal"] = DocumentTexts.Amount(model.LinesTotal, model.Currency, lang),
                ["extradiscount"] = model.ExtraDiscountAmount == 0 ? "" :
                    model.ExtraDiscountPercent.ToString("0.##", culture) + " %  -" + DocumentTexts.Amount(model.ExtraDiscountAmount, model.Currency, lang),
                ["freight"] = DocumentTexts.Amount(model.Freight, model.Currency, lang),
                ["total"] = DocumentTexts.Amount(model.Total, model.Currency, lang),
                ["validity"] = model.ValidUntil.HasValue ? string.Format(DocumentTexts.Get(lang, "validity"), DocumentTexts.Date(model.ValidUntil.Value, lang)) : "",
                ["leadtime"] = model.EarliestDelivery.HasValue
                    ? string.Format(DocumentTexts.Get(lang, "leadtime"), LeadTimeCalculator.IsoWeek(model.EarliestDelivery.Value), model.EarliestDelivery.Value.Year) : "",
                ["confirmed"] = string.Format(DocumentTexts.Get(lang, "confirmed"), model.DealerReference ?? model.Number),
                ["warranty"] = DocumentTexts.Get(lang, "warranty"),
                ["notes"] = model.Notes ?? ""
            };

            var lines = model.Lines.OrderBy(l => l.LineNumber).Select(l => (IDictionary<string, string>)new Dictionary<string, string>
            {
                ["no"] = l.LineNumber.ToString(CultureInfo.InvariantCulture),
                ["item"] = l.ItemId,
                ["description"] = l.Description + (l.OptionDescriptions.Count == 0 ? "" : "\n" + string.Join("\n", l.OptionDescriptions.Select(o => "  - " + o))),
                ["qty"] = l.Quantity.ToString("0.##", culture),
                ["list"] = DocumentTexts.Amount(l.ListPrice, model.Currency, lang),
                ["discount"] = l.DiscountPercent == 0 ? "" : l.DiscountPercent.ToString("0.##", culture) + " %",
                ["net"] = DocumentTexts.Amount(l.NetAmount, model.Currency, lang),
                ["shipdate"] = l.ShipDate.HasValue ? DocumentTexts.Date(l.ShipDate.Value, lang) : ""
            }).ToList();

            var flags = new Dictionary<string, bool>
            {
                ["extradiscount"] = model.ExtraDiscountAmount != 0,
                ["endcustomer"] = !string.IsNullOrEmpty(model.EndCustomer),
                ["notes"] = !string.IsNullOrEmpty(model.Notes)
            };

            var template = new WordTemplate(_templateLoader(TemplateName(kind, lang)));
            return template.Fill(fields, new Dictionary<string, IList<IDictionary<string, string>>> { ["line"] = lines }, flags);
        }

        public static string TemplateName(string kind, string language) => kind + "." + (language ?? "en").ToLowerInvariant() + ".docx";

        private static string DeliveryText(DeliveryTerms terms, string lang) => DocumentTexts.Get(lang, "delivery." + terms.ToString().ToLowerInvariant());
    }
}
