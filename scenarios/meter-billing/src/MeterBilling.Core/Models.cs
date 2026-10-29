using System;
using System.Collections.Generic;

namespace Coho.Billing.Core
{
    public enum ReadType
    {
        Actual = 0,
        Customer = 1,
        Estimated = 2
    }

    public enum ReadSource
    {
        Handheld = 0,
        Amr = 1,
        Customer = 2,
        Engine = 3
    }

    public enum PaymentMethod
    {
        Invoice = 0,
        DirectDebit = 1
    }

    public class MeterRead
    {
        public string MeterSerial { get; set; }
        public DateTime ReadDate { get; set; }
        public decimal Reading { get; set; }
        public ReadType ReadType { get; set; }
        public ReadSource Source { get; set; }

        /// <summary>Reader or device code from the file: 00 = normal, NA = no access, DM = damaged, LK = leak suspected.</summary>
        public string ReaderCode { get; set; }
        public string Route { get; set; }
        public string ReaderId { get; set; }
    }

    public class Meter
    {
        public string Serial { get; set; }

        /// <summary>Number of whole-number dials on the register; the register rolls over at 10^Dials.</summary>
        public int Dials { get; set; }
        public decimal Multiplier { get; set; } = 1m;
        public string SizeCode { get; set; }
    }

    public class BillingAccount
    {
        public string AccountNumber { get; set; }
        public string CustomerName { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string TariffCode { get; set; }
        public string CycleCode { get; set; }
        public Meter Meter { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string Iban { get; set; }
        public string MandateId { get; set; }
        public DateTime? MandateSignedOn { get; set; }
        public bool FirstCollection { get; set; }

        /// <summary>Average daily consumption in m3 over the last twelve months; used for checks and estimates.</summary>
        public decimal AverageDailyUse { get; set; }
        public MeterRead LastBilledRead { get; set; }
    }

    public class BillLine
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public decimal VatRate { get; set; }
    }

    public class Bill
    {
        public Bill()
        {
            Lines = new List<BillLine>();
        }

        public string BillNumber { get; set; }
        public string AccountNumber { get; set; }
        public DateTime BillDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public decimal OpeningReading { get; set; }
        public decimal ClosingReading { get; set; }
        public decimal Consumption { get; set; }
        public bool Estimated { get; set; }
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentReference { get; set; }
        public List<BillLine> Lines { get; private set; }
    }
}
