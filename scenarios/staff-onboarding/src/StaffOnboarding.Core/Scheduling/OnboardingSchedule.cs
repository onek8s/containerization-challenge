using System;

namespace StaffOnboarding.Core.Scheduling
{
    /// <summary>
    /// Calculates when workflow steps are due. All inputs and outputs are local office time;
    /// <see cref="ToUtc"/> converts for timers.
    /// </summary>
    public class OnboardingSchedule
    {
        private readonly BusinessCalendar _calendar;
        private readonly ScheduleOptions _options;
        private readonly TimeZoneInfo _timeZone;

        public OnboardingSchedule(BusinessCalendar calendar, ScheduleOptions options, TimeZoneInfo timeZone)
        {
            if (calendar == null) throw new ArgumentNullException("calendar");
            if (options == null) throw new ArgumentNullException("options");
            if (timeZone == null) throw new ArgumentNullException("timeZone");
            _calendar = calendar;
            _options = options;
            _timeZone = timeZone;
        }

        public ScheduleOptions Options
        {
            get { return _options; }
        }

        /// <summary>Earliest start date HR may enter for a joiner submitted at <paramref name="submittedLocal"/>.</summary>
        public DateTime EarliestStartDate(DateTime submittedLocal)
        {
            return _calendar.AddBusinessDays(submittedLocal.Date, _options.ProvisioningLeadBusinessDays + 2);
        }

        public DateTime JoinerProvisioningTime(DateTime startDate)
        {
            var day = _calendar.AddBusinessDays(_calendar.NextBusinessDay(startDate), -_options.ProvisioningLeadBusinessDays);
            return day + _options.ProvisioningTimeOfDay;
        }

        /// <summary>The account is enabled on the first business day at or after the start date.</summary>
        public DateTime JoinerEnableTime(DateTime startDate)
        {
            return _calendar.NextBusinessDay(startDate) + _options.ProvisioningTimeOfDay;
        }

        public DateTime MoverApplyTime(DateTime effectiveDate)
        {
            return effectiveDate.Date + _options.ProvisioningTimeOfDay;
        }

        public DateTime MoverRemoveOldAccessTime(DateTime effectiveDate, int handoverDays)
        {
            if (handoverDays < 0) throw new ArgumentOutOfRangeException("handoverDays");
            return _calendar.AddBusinessDays(effectiveDate.Date, handoverDays) + _options.ProvisioningTimeOfDay;
        }

        public DateTime LeaverDisableTime(DateTime lastWorkingDay)
        {
            return lastWorkingDay.Date + _options.LeaverDisableTimeOfDay;
        }

        public DateTime LeaverCleanupTime(DateTime lastWorkingDay)
        {
            return LeaverDisableTime(lastWorkingDay) + _options.CleanupDelay;
        }

        /// <summary>Value for accountExpires: the end of the last contract day.</summary>
        public DateTime? AccountExpiry(DateTime? contractEndDate)
        {
            if (!contractEndDate.HasValue) return null;
            return contractEndDate.Value.Date.AddDays(1);
        }

        public DateTime ToUtc(DateTime local)
        {
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _timeZone);
        }

        public DateTime ToLocal(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);
        }

        /// <summary>Delay until <paramref name="dueLocal"/>, never negative.</summary>
        public TimeSpan DelayUntil(DateTime dueLocal, DateTime nowUtc)
        {
            var delay = ToUtc(dueLocal) - nowUtc;
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }
    }
}
