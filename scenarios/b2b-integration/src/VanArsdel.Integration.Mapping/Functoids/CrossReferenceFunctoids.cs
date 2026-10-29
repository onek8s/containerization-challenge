using System;
using VanArsdel.Integration.Domain.Codes;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.Mapping.Functoids
{
    /// <summary>
    /// Scripting functoid class: partner and item cross-references. Unresolved items are returned with the
    /// prefix "?" so the order validation policy rejects the line with a readable reason instead of the ERP failing
    /// on an unknown material.
    /// </summary>
    public class CrossReferenceFunctoids
    {
        public const string UnresolvedPrefix = "?";

        private static IXrefProvider Provider
        {
            get { return XrefProviderLocator.Current; }
        }

        public string PartnerIdFromInterchange(string qualifier, string id)
        {
            var p = Provider.PartnerByInterchange(qualifier, id);
            return p == null ? UnresolvedPrefix + (id ?? string.Empty).Trim() : p.PartnerId;
        }

        public string PartnerIdFromGln(string gln)
        {
            var p = Provider.PartnerByInterchange("14", gln);
            return p == null ? UnresolvedPrefix + gln : p.PartnerId;
        }

        public string PartnerIdFromErpCustomer(string erpNumber)
        {
            var p = Provider.PartnerByErpNumber(PartnerRole.Customer, erpNumber);
            return p == null ? UnresolvedPrefix + erpNumber : p.PartnerId;
        }

        public string PartnerIdFromErpVendor(string erpNumber)
        {
            var p = Provider.PartnerByErpNumber(PartnerRole.Supplier, erpNumber);
            return p == null ? UnresolvedPrefix + erpNumber : p.PartnerId;
        }

        public string ErpNumber(string partnerId)
        {
            var p = Provider.Partner(partnerId);
            return p == null ? string.Empty : p.ErpNumber ?? string.Empty;
        }

        public string Gln(string partnerId)
        {
            var p = Provider.Partner(partnerId);
            return p == null ? string.Empty : p.Gln ?? string.Empty;
        }

        public string Currency(string partnerId)
        {
            var p = Provider.Partner(partnerId);
            return p == null || string.IsNullOrEmpty(p.CurrencyCode) ? "USD" : p.CurrencyCode;
        }

        public string PartnerName(string partnerId)
        {
            var p = Provider.Partner(partnerId);
            return p == null ? string.Empty : p.Name ?? string.Empty;
        }

        /// <summary>Partner item number (buyer part number) or GTIN to our material number.</summary>
        public string ItemToInternal(string partnerId, string partnerItem, string gtin)
        {
            if (!string.IsNullOrWhiteSpace(partnerItem))
            {
                var m = Provider.ToInternal(partnerId, CrossReferenceKind.Item, partnerItem);
                if (m != null)
                {
                    return m;
                }
            }

            var g = CheckDigit.ToGtin14((gtin ?? string.Empty).Trim());
            if (g != null)
            {
                var m = Provider.ToInternal("*", CrossReferenceKind.Item, g);
                if (m != null)
                {
                    return m;
                }
            }

            return UnresolvedPrefix + (string.IsNullOrWhiteSpace(partnerItem) ? gtin : partnerItem);
        }

        public string ItemToPartner(string partnerId, string internalItem)
        {
            var back = Provider.ToPartner(partnerId, CrossReferenceKind.Item, internalItem);
            return back ?? string.Empty;
        }

        public string GtinOfItem(string internalItem)
        {
            var g = Provider.ToPartner("*", CrossReferenceKind.Item, internalItem);
            return g ?? string.Empty;
        }

        public string ShipToInternal(string partnerId, string partnerShipTo)
        {
            if (string.IsNullOrWhiteSpace(partnerShipTo))
            {
                return string.Empty;
            }

            return Provider.ToInternal(partnerId, CrossReferenceKind.ShipTo, partnerShipTo) ?? UnresolvedPrefix + partnerShipTo;
        }

        public string ShipToPartner(string partnerId, string internalShipTo)
        {
            return Provider.ToPartner(partnerId, CrossReferenceKind.ShipTo, internalShipTo) ?? internalShipTo;
        }

        /// <summary>Carrier code of the WMS (e.g. UPSGND) to the partner's SCAC or EDIFACT carrier id.</summary>
        public string CarrierToPartner(string partnerId, string carrier)
        {
            return Provider.ToPartner(partnerId, CrossReferenceKind.Carrier, carrier) ?? carrier;
        }

        public string IsUnresolved(string value)
        {
            return (!string.IsNullOrEmpty(value) && value.StartsWith(UnresolvedPrefix, StringComparison.Ordinal)) ? "true" : "false";
        }
    }
}
