using System;
using Microsoft.Extensions.Configuration;

namespace ProductReturns.Api.Services
{
    public interface IBusinessClock
    {
        DateTime UtcNow { get; }

        /// <summary>Today in the head office time zone; return windows are counted in local days.</summary>
        DateTime Today { get; }
    }

    public class BusinessClock : IBusinessClock
    {
        private readonly TimeZoneInfo _zone;

        public BusinessClock(IConfiguration configuration)
        {
            _zone = TimeZoneInfo.FindSystemTimeZoneById(configuration["Business:TimeZone"] ?? "W. Europe Standard Time");
        }

        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone).Date;
    }
}
