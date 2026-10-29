using System;

namespace Tailspin.DealerOrders.Processing.Ports
{
    public class SystemClock : IClock
    {
        private readonly TimeZoneInfo _factoryZone;

        /// <param name="factoryTimeZoneId">Time zone of the factory, "FLE Standard Time" on the servers.</param>
        public SystemClock(string factoryTimeZoneId)
        {
            _factoryZone = TimeZoneInfo.FindSystemTimeZoneById(factoryTimeZoneId);
        }

        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _factoryZone).Date;
    }
}
