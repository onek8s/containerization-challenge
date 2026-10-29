using System;
using System.Collections.Generic;
using Fabrikam.Housing.Tenancies;

namespace Fabrikam.Housing.Rent
{
    /// <summary>
    /// Builds the charge lines for one rent week. The job runs every Monday morning and is idempotent:
    /// a tenancy that already has a rent charge for the week is skipped by the caller.
    /// </summary>
    public class RentChargeRun
    {
        private readonly RentCalendar _calendar;

        public RentChargeRun(RentCalendar calendar)
        {
            _calendar = calendar;
        }

        public IEnumerable<RentTransaction> ChargesFor(Tenancy tenancy, DateTime rentWeek, DateTime postedOn)
        {
            var week = RentCalendar.WeekStart(rentWeek);
            if (_calendar.IsRentFreeWeek(week)) yield break;
            if (tenancy.Status == TenancyStatus.Ended && tenancy.EndDate.HasValue && tenancy.EndDate.Value < week) yield break;
            if (tenancy.StartDate.Date > week.AddDays(6)) yield break;

            var property = tenancy.Property;
            var weeks = RentCalendar.WeeksInRentYear(RentCalendar.RentYearStartFor(week));
            var factor = ProRata(tenancy, week);

            yield return Line(tenancy, week, postedOn, TransactionType.RentCharge, RentCalendar.ChargingWeekAmount(property.WeeklyNetRent, weeks) * factor, "Rent");
            if (property.WeeklyServiceCharge > 0)
                yield return Line(tenancy, week, postedOn, TransactionType.ServiceCharge, RentCalendar.ChargingWeekAmount(property.WeeklyServiceCharge, weeks) * factor, "Service charge");
            if (property.WeeklyWaterCharge > 0)
                yield return Line(tenancy, week, postedOn, TransactionType.WaterCharge, RentCalendar.ChargingWeekAmount(property.WeeklyWaterCharge, weeks) * factor, "Water and sewerage");
        }

        /// <summary>Part weeks at the start or end of a tenancy are charged by the day.</summary>
        private static decimal ProRata(Tenancy tenancy, DateTime week)
        {
            var first = tenancy.StartDate.Date > week ? tenancy.StartDate.Date : week;
            var last = tenancy.EndDate.HasValue && tenancy.EndDate.Value.Date < week.AddDays(6) ? tenancy.EndDate.Value.Date : week.AddDays(6);
            var days = (last - first).Days + 1;
            return days >= 7 ? 1m : Math.Max(0, days) / 7m;
        }

        private static RentTransaction Line(Tenancy tenancy, DateTime week, DateTime postedOn, TransactionType type, decimal amount, string description)
        {
            return new RentTransaction
            {
                TenancyId = tenancy.Id,
                RentWeek = week,
                PostedOn = postedOn,
                Type = type,
                Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
                Reference = "WK" + RentCalendar.WeekNumber(week).ToString("00") + "/" + RentCalendar.RentYearStartFor(week).Year,
                Description = description,
                Source = "RentRun"
            };
        }
    }
}
