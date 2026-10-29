using System;
using System.Collections.Generic;
using System.Linq;
using VanArsdel.Integration.Domain.Orders;

namespace VanArsdel.Integration.Monitoring
{
    public class PartnerKpi
    {
        public string PartnerId { get; set; }
        public int Orders { get; set; }
        public int Rejected { get; set; }
        public int CreditHolds { get; set; }
        public double? MedianMinutesToErp { get; set; }
        public double? AverageHoursToDespatch { get; set; }
        public int Interchanges { get; set; }
        public int AcksOnTime { get; set; }
        public int AcksLateOrMissing { get; set; }

        public double RejectRate
        {
            get { return Orders == 0 ? 0 : (double)Rejected / Orders; }
        }

        public double AckOnTimeRate
        {
            get { return Interchanges == 0 ? 1 : (double)AcksOnTime / Interchanges; }
        }
    }

    /// <summary>Partner scorecard of the tracking portal (BAM portal replacement), computed from the BAM views.</summary>
    public static class PartnerKpiCalculator
    {
        public static IList<PartnerKpi> Calculate(IEnumerable<OrderActivityRow> orders, IEnumerable<InterchangeRecord> outbound)
        {
            var o = orders.ToList();
            var i = outbound.ToList();
            var partners = o.Select(x => x.PartnerId).Concat(i.Select(x => x.PartnerId)).Where(p => p != null).Distinct().OrderBy(p => p);
            return partners.Select(p =>
            {
                var po = o.Where(x => x.PartnerId == p).ToList();
                var pi = i.Where(x => x.PartnerId == p && x.AckStatus != AckStatus.NotRequired).ToList();
                var toErp = po.Where(x => x.ReceivedUtc.HasValue && x.SentToErpUtc.HasValue)
                    .Select(x => (x.SentToErpUtc.Value - x.ReceivedUtc.Value).TotalMinutes).OrderBy(m => m).ToList();
                var toDespatch = po.Where(x => x.ReceivedUtc.HasValue && x.DespatchedUtc.HasValue)
                    .Select(x => (x.DespatchedUtc.Value - x.ReceivedUtc.Value).TotalHours).ToList();
                return new PartnerKpi
                {
                    PartnerId = p,
                    Orders = po.Count,
                    Rejected = po.Count(x => x.RejectedUtc.HasValue),
                    CreditHolds = po.Count(x => x.CreditHoldUtc.HasValue),
                    MedianMinutesToErp = toErp.Count == 0 ? (double?)null : Median(toErp),
                    AverageHoursToDespatch = toDespatch.Count == 0 ? (double?)null : Math.Round(toDespatch.Average(), 1),
                    Interchanges = pi.Count,
                    AcksOnTime = pi.Count(x => x.AckReceivedUtc.HasValue && (!x.AckDueUtc.HasValue || x.AckReceivedUtc <= x.AckDueUtc)),
                    AcksLateOrMissing = pi.Count(x => !x.AckReceivedUtc.HasValue || (x.AckDueUtc.HasValue && x.AckReceivedUtc > x.AckDueUtc))
                };
            }).ToList();
        }

        private static double Median(IList<double> sorted)
        {
            var n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }
    }
}
