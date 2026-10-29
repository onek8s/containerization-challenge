using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Coho.Billing.Core.Rating
{
    public class BillingPeriod
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public decimal OpeningReading { get; set; }
        public decimal ClosingReading { get; set; }
        public decimal Consumption { get; set; }
        public bool Estimated { get; set; }
    }

    /// <summary>
    /// Rates a billing period against the tariff versions in force. When the tariff changes
    /// inside the period, consumption is apportioned by days and each part is rated with
    /// its own version; block limits are scaled from 30 days to the days in each part.
    /// </summary>
    public class BillCalculator
    {
        public const int BlockBasisDays = 30;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public Bill Calculate(BillingAccount account, BillingPeriod period, IEnumerable<Tariff> tariffVersions, DateTime billDate, int paymentTermDays)
        {
            if (account == null) throw new ArgumentNullException("account");
            if (period == null) throw new ArgumentNullException("period");

            var totalDays = (period.To.Date - period.From.Date).Days;
            if (totalDays <= 0)
            {
                throw new ArgumentException("Billing period must be at least one day", "period");
            }

            var versions = tariffVersions
                .Where(t => string.Equals(t.Code, account.TariffCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.ValidFrom)
                .ToList();

            var parts = Split(period, versions);

            var bill = new Bill
            {
                AccountNumber = account.AccountNumber,
                BillDate = billDate.Date,
                DueDate = billDate.Date.AddDays(paymentTermDays),
                PeriodFrom = period.From.Date,
                PeriodTo = period.To.Date,
                OpeningReading = period.OpeningReading,
                ClosingReading = period.ClosingReading,
                Consumption = period.Consumption,
                Estimated = period.Estimated
            };

            var allocated = 0m;
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                decimal partConsumption;
                if (i == parts.Count - 1)
                {
                    partConsumption = period.Consumption - allocated;
                }
                else
                {
                    partConsumption = Math.Round(period.Consumption * part.Days / totalDays, 3, MidpointRounding.AwayFromZero);
                    allocated += partConsumption;
                }

                var suffix = parts.Count > 1
                    ? string.Format(Invariant, " {0:dd.MM.}-{1:dd.MM.yyyy}", part.From, part.To.AddDays(-1))
                    : string.Empty;

                AddStandingCharge(bill, part, suffix);
                AddWater(bill, part, partConsumption, suffix);
                AddWastewater(bill, part, partConsumption, suffix);
            }

            bill.NetAmount = bill.Lines.Sum(l => l.Amount);
            bill.VatAmount = bill.Lines
                .GroupBy(l => l.VatRate)
                .Sum(g => Money.Round(g.Sum(l => l.Amount) * g.Key / 100m));
            bill.TotalAmount = bill.NetAmount + bill.VatAmount;
            return bill;
        }

        private static void AddStandingCharge(Bill bill, Part part, string suffix)
        {
            if (part.Tariff.StandingChargePerDay == 0)
            {
                return;
            }

            bill.Lines.Add(new BillLine
            {
                Code = "BASIC",
                Description = "Basic charge" + suffix,
                Quantity = part.Days,
                Unit = "d",
                UnitPrice = part.Tariff.StandingChargePerDay,
                Amount = Money.Round(part.Days * part.Tariff.StandingChargePerDay),
                VatRate = part.Tariff.VatRate
            });
        }

        private static void AddWater(Bill bill, Part part, decimal consumption, string suffix)
        {
            var remaining = consumption;
            decimal lower = 0;
            var blockNo = 1;
            foreach (var block in part.Tariff.WaterBlocks)
            {
                if (remaining <= 0)
                {
                    break;
                }

                decimal inBlock;
                if (block.UpTo.HasValue)
                {
                    var upper = Math.Round(block.UpTo.Value * part.Days / BlockBasisDays, 3, MidpointRounding.AwayFromZero);
                    inBlock = Math.Min(remaining, upper - lower);
                    lower = upper;
                }
                else
                {
                    inBlock = remaining;
                }

                if (inBlock <= 0)
                {
                    continue;
                }

                remaining -= inBlock;
                bill.Lines.Add(new BillLine
                {
                    Code = "WATER" + blockNo.ToString(Invariant),
                    Description = (part.Tariff.WaterBlocks.Count > 1 ? "Water, block " + blockNo.ToString(Invariant) : "Water") + suffix,
                    Quantity = inBlock,
                    Unit = "m3",
                    UnitPrice = block.Price,
                    Amount = Money.Round(inBlock * block.Price),
                    VatRate = part.Tariff.VatRate
                });
                blockNo++;
            }
        }

        private static void AddWastewater(Bill bill, Part part, decimal consumption, string suffix)
        {
            var quantity = Math.Round(consumption * part.Tariff.WastewaterShare, 3, MidpointRounding.AwayFromZero);
            if (quantity <= 0 || part.Tariff.WastewaterPrice == 0)
            {
                return;
            }

            bill.Lines.Add(new BillLine
            {
                Code = "WASTE",
                Description = "Wastewater" + suffix,
                Quantity = quantity,
                Unit = "m3",
                UnitPrice = part.Tariff.WastewaterPrice,
                Amount = Money.Round(quantity * part.Tariff.WastewaterPrice),
                VatRate = part.Tariff.VatRate
            });
        }

        private static List<Part> Split(BillingPeriod period, List<Tariff> versions)
        {
            var from = period.From.Date;
            var to = period.To.Date;

            var first = versions.LastOrDefault(v => v.ValidFrom <= from);
            if (first == null)
            {
                throw new InvalidOperationException("No tariff in force on " + from.ToString("yyyy-MM-dd", Invariant));
            }

            var parts = new List<Part>();
            var current = first;
            var start = from;
            foreach (var next in versions.Where(v => v.ValidFrom > from && v.ValidFrom < to))
            {
                parts.Add(new Part(current, start, next.ValidFrom));
                current = next;
                start = next.ValidFrom;
            }

            parts.Add(new Part(current, start, to));
            return parts;
        }

        private class Part
        {
            public Part(Tariff tariff, DateTime from, DateTime to)
            {
                Tariff = tariff;
                From = from;
                To = to;
            }

            public Tariff Tariff { get; private set; }
            public DateTime From { get; private set; }
            public DateTime To { get; private set; }

            public int Days
            {
                get { return (To - From).Days; }
            }
        }
    }
}
