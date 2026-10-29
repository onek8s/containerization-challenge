using System;

namespace VanArsdel.Integration.Domain.Partners
{
    public enum CrossReferenceKind
    {
        Item = 1,
        UnitOfMeasure = 2,
        ShipTo = 3,
        Warehouse = 4,
        Carrier = 5,
        PaymentTerms = 6
    }

    /// <summary>Partner value to internal (ERP) value, one row of dbo.PartnerXref.</summary>
    public class PartnerCrossReference
    {
        public string PartnerId { get; set; }
        public CrossReferenceKind Kind { get; set; }
        public string PartnerValue { get; set; }
        public string InternalValue { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        public bool IsValidOn(DateTime date)
        {
            return (!ValidFrom.HasValue || ValidFrom.Value.Date <= date.Date)
                   && (!ValidTo.HasValue || ValidTo.Value.Date >= date.Date);
        }

        public static string Key(string partnerId, CrossReferenceKind kind, string partnerValue)
        {
            return string.Concat(
                TradingPartner.NormalizeId(partnerId), "|",
                ((int)kind).ToString(System.Globalization.CultureInfo.InvariantCulture), "|",
                (partnerValue ?? string.Empty).Trim().ToUpperInvariant());
        }
    }
}
