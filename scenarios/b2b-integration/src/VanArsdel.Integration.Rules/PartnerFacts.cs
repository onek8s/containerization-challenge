using System;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.Rules
{
    /// <summary>Partner master data and the ERP credit answer, asserted with the order.</summary>
    [Serializable]
    public class PartnerFacts
    {
        public PartnerFacts(TradingPartner partner)
        {
            if (partner == null)
            {
                throw new ArgumentNullException("partner");
            }

            PartnerId = partner.PartnerId;
            Standard = partner.Standard.ToString();
            Transport = partner.Transport.ToString();
            DefaultShipVia = partner.DefaultShipVia ?? string.Empty;
            CreditCheckThreshold = partner.CreditCheckThreshold;
            Active = partner.Active;
            CountryCode = partner.CountryCode ?? string.Empty;
        }

        public string PartnerId { get; private set; }
        public string Standard { get; private set; }
        public string Transport { get; private set; }
        public string DefaultShipVia { get; private set; }
        public decimal CreditCheckThreshold { get; private set; }
        public bool Active { get; private set; }
        public string CountryCode { get; private set; }

        /// <summary>Set from the ERP CheckCredit response before the second policy pass.</summary>
        public bool CreditChecked { get; set; }

        public bool CreditApproved { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal OpenExposure { get; set; }
        public string BlockReason { get; set; }

        public decimal AvailableCredit()
        {
            return CreditLimit - OpenExposure;
        }

        public bool IsInternational()
        {
            return CountryCode.Length > 0 && CountryCode != "US";
        }
    }
}
