using System;

namespace Woodgrove.Statements.Core.Delivery
{
    public static class DeliveryStatus
    {
        public const string Pending = "Pending";
        public const string Sent = "Sent";
        public const string Retry = "Retry";
        public const string Failed = "Failed";
        public const string Skipped = "Skipped";
    }

    public class RetryPolicy
    {
        public int MaxAttempts { get; set; } = 4;
        public TimeSpan FirstDelay { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromHours(4);

        /// <summary>Next attempt after a failed attempt number <paramref name="attempt"/> (1-based), or null when giving up.</summary>
        public DateTime? NextAttempt(int attempt, DateTime failedAt)
        {
            if (attempt >= MaxAttempts) return null;
            var delay = TimeSpan.FromTicks(FirstDelay.Ticks * (1L << Math.Min(attempt - 1, 20)));
            if (delay > MaxDelay) delay = MaxDelay;
            return failedAt + delay;
        }
    }
}
