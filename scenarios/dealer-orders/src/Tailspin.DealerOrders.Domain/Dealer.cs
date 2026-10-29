using System;
using System.Collections.Generic;

namespace Tailspin.DealerOrders.Domain
{
    public class Dealer
    {
        public Dealer()
        {
            Addresses = new List<DealerAddress>();
        }

        public int Id { get; set; }

        /// <summary>AX customer account (CustTable.AccountNum), e.g. D-10045.</summary>
        public string AccountNumber { get; set; }

        public string Name { get; set; }
        public string CountryCode { get; set; }
        public string Currency { get; set; }
        public string LanguageCode { get; set; }

        /// <summary>AX price group (PriceDiscGroup) used for trade agreements.</summary>
        public string PriceGroup { get; set; }

        public DiscountTier DiscountTier { get; set; }

        /// <summary>Highest extra discount (percent) a dealer may enter on a quote without sales desk approval.</summary>
        public decimal MaxSelfApprovedDiscount { get; set; }

        public decimal CreditLimit { get; set; }

        /// <summary>Open AR balance from AX (CustTrans), refreshed by the sync service.</summary>
        public decimal OpenBalance { get; set; }

        public bool CreditHold { get; set; }
        public string PaymentTerms { get; set; }
        public DeliveryTerms DeliveryTerms { get; set; }

        /// <summary>Default AX warehouse (InventLocationId) that ships to this dealer.</summary>
        public string Warehouse { get; set; }

        public string SalesDistrict { get; set; }
        public string SalesResponsible { get; set; }
        public string MainDealerAccount { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public DealerStatus Status { get; set; }
        public int? EdiPartnerId { get; set; }
        public DateTime? LastSyncedUtc { get; set; }
        public byte[] RowVersion { get; set; }

        public virtual ICollection<DealerAddress> Addresses { get; set; }

        public bool CanOrder => Status == DealerStatus.Active;

        /// <summary>Credit still available given the open balance and the value of open orders not yet invoiced.</summary>
        public decimal AvailableCredit(decimal openOrderValue) => CreditLimit - OpenBalance - openOrderValue;
    }

    public class DealerAddress
    {
        public int Id { get; set; }
        public int DealerId { get; set; }

        /// <summary>AX logistics location id; empty until the address has been created in AX.</summary>
        public string AxLocationId { get; set; }

        public string Name { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string CountryCode { get; set; }
        public bool IsDefaultDelivery { get; set; }

        /// <summary>Ship-to code used by the dealer in EDI messages (N1*ST*...*92*code).</summary>
        public string EdiLocationCode { get; set; }

        public override string ToString() =>
            string.Join(", ", new[] { Name, Street, (PostalCode + " " + City).Trim(), State, CountryCode }).Replace(", , ", ", ").Trim(' ', ',');
    }
}
