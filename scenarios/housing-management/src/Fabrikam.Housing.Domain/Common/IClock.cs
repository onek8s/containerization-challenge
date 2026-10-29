using System;

namespace Fabrikam.Housing.Common
{
    /// <summary>
    /// Local (UK) time. All rent weeks, SLA targets and letters are in UK local time.
    /// </summary>
    public interface IClock
    {
        DateTime Now { get; }
        DateTime Today { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
        public DateTime Today => DateTime.Today;
    }

    public class FixedClock : IClock
    {
        public FixedClock(DateTime now) { Now = now; }
        public DateTime Now { get; set; }
        public DateTime Today => Now.Date;
    }
}
