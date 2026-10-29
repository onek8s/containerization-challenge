using System;
using Tailspin.CustomerCare.Data;

namespace Tailspin.CustomerCare.Web.Services
{
    /// <summary>All times are stored in UTC and shown in the contact centre's time zone.</summary>
    public static class LocalTime
    {
        public static DateTime Now
        {
            get { return ReferenceData.Calendar.ToLocal(DateTime.UtcNow); }
        }

        public static DateTime TodayStartUtc
        {
            get { return ReferenceData.Calendar.ToUtc(Now.Date); }
        }

        public static string Format(DateTime utc)
        {
            return ReferenceData.Calendar.ToLocal(utc).ToString("dd.MM.yyyy HH:mm");
        }

        public static string Format(DateTime? utc)
        {
            return utc.HasValue ? Format(utc.Value) : string.Empty;
        }

        public static string Short(DateTime utc)
        {
            var local = ReferenceData.Calendar.ToLocal(utc);
            return local.Date == Now.Date ? local.ToString("HH:mm") : local.ToString("dd.MM. HH:mm");
        }
    }
}
