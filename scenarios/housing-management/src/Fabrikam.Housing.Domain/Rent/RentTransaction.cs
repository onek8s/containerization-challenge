using System;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Tenancies;

namespace Fabrikam.Housing.Rent
{
    public enum TransactionType
    {
        RentCharge = 1,
        ServiceCharge = 2,
        WaterCharge = 3,
        CardPayment = 10,
        DirectDebit = 11,
        DirectDebitReturned = 12,
        HousingBenefit = 13,
        UniversalCreditDirect = 14,
        CashPayment = 15,
        Adjustment = 20,
        WriteOff = 21,
        Refund = 22,
        RechargeRepair = 23
    }

    /// <summary>
    /// A line on the rent account. Positive amounts are debits (owed by the tenant), negative amounts are credits.
    /// </summary>
    public class RentTransaction : Entity
    {
        public int TenancyId { get; set; }
        public Tenancy Tenancy { get; set; }
        public DateTime PostedOn { get; set; }
        /// <summary>Monday of the rent week the line belongs to.</summary>
        public DateTime RentWeek { get; set; }
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        /// <summary>Set when the finance export has picked the line up.</summary>
        public int? JournalBatchId { get; set; }

        public bool IsDebit => Amount > 0;

        public static bool IsPayment(TransactionType type) =>
            type == TransactionType.CardPayment || type == TransactionType.DirectDebit || type == TransactionType.HousingBenefit
            || type == TransactionType.UniversalCreditDirect || type == TransactionType.CashPayment;
    }
}
