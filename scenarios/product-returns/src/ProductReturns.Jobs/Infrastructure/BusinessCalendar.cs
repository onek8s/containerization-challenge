using System;
using Microsoft.Extensions.Options;

namespace ProductReturns.Jobs.Infrastructure
{
    public class BusinessCalendar
    {
        private readonly TimeZoneInfo _zone;

        public BusinessCalendar(IOptions<JobOptions> options)
        {
            _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        }

        public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);

        /// <summary>Start of today in the head office, as UTC.</summary>
        public DateTime TodayStartUtc => TimeZoneInfo.ConvertTimeToUtc(LocalNow.Date, _zone);
    }
}
