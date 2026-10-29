using System;

namespace VanArsdel.Integration.Domain.Partners
{
    public enum PartnerRole
    {
        Customer = 1,
        Supplier = 2,
        Logistics = 3
    }

    public enum DocumentStandard
    {
        X12 = 1,
        Edifact = 2,
        Xml = 3
    }

    public enum TransportKind
    {
        As2 = 1,
        Sftp = 2,
        Ftp = 3,
        Gateway = 4,
        File = 5
    }

    /// <summary>
    /// A customer or supplier we exchange documents with. Mirrors dbo.TradingPartner and the BizTalk party of the
    /// same name (the party name is always the PartnerId).
    /// </summary>
    public class TradingPartner
    {
        public string PartnerId { get; set; }
        public string Name { get; set; }
        public PartnerRole Role { get; set; }
        public DocumentStandard Standard { get; set; }
        public TransportKind Transport { get; set; }

        /// <summary>ISA06/ISA08 or UNB S002/S003 identification.</summary>
        public string InterchangeId { get; set; }

        /// <summary>X12 ISA05/ISA07 qualifier (ZZ, 01, 12) or EDIFACT code list (14 = GLN, ZZZ = mutually defined).</summary>
        public string InterchangeQualifier { get; set; }

        /// <summary>X12 version (00401) or EDIFACT directory (D96A).</summary>
        public string Version { get; set; }

        public string Gln { get; set; }
        public string ErpNumber { get; set; }
        public string As2Identifier { get; set; }
        public string CurrencyCode { get; set; }
        public string CountryCode { get; set; }
        public string DefaultShipVia { get; set; }

        public bool AckRequired { get; set; }
        public int AckTimeoutMinutes { get; set; }

        /// <summary>Orders over this value need a credit check in the ERP before release.</summary>
        public decimal CreditCheckThreshold { get; set; }

        public bool Active { get; set; }
        public bool TestIndicator { get; set; }

        public bool IsCustomer
        {
            get { return Role == PartnerRole.Customer; }
        }

        public bool IsSupplier
        {
            get { return Role == PartnerRole.Supplier; }
        }

        public override string ToString()
        {
            return PartnerId + " " + Name;
        }

        public static string NormalizeId(string value)
        {
            return value == null ? null : value.Trim().ToUpperInvariant();
        }

        public bool MatchesInterchange(string qualifier, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            var sameId = string.Equals(NormalizeId(InterchangeId), NormalizeId(id), StringComparison.Ordinal);
            if (!sameId)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(qualifier)
                   || string.IsNullOrWhiteSpace(InterchangeQualifier)
                   || string.Equals(InterchangeQualifier.Trim(), qualifier.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
