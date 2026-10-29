using System;

namespace Northwind.Inspections.Core
{
    public static class ReportSchedule
    {
        public const int MaxAttempts = 6;

        private static readonly int[] BackoffMinutes = { 2, 5, 15, 30, 60, 120 };

        /// <summary>When a failed report should be retried, or null when it should be abandoned.</summary>
        public static DateTime? NextAttemptUtc(int attemptsSoFar, DateTime nowUtc)
        {
            if (attemptsSoFar >= MaxAttempts)
            {
                return null;
            }
            var index = Math.Max(0, Math.Min(attemptsSoFar - 1, BackoffMinutes.Length - 1));
            return nowUtc.AddMinutes(BackoffMinutes[index]);
        }

        /// <summary>A lock older than this is considered abandoned by a crashed job run.</summary>
        public static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(20);

        /// <summary>Photos of closed or reported inspections are removed from the share after the retention period.</summary>
        public static bool IsPurgeDue(Inspection inspection, DateTime nowUtc, int retentionDays)
        {
            if (inspection == null || retentionDays <= 0)
            {
                return false;
            }
            if (inspection.Status != InspectionStatus.ReportSent && inspection.Status != InspectionStatus.Closed)
            {
                return false;
            }
            var reference = inspection.ReportSentUtc ?? inspection.CreatedUtc;
            return reference.AddDays(retentionDays) <= nowUtc;
        }

        public static bool CanRequestReport(Inspection inspection, int photoCount, out string reason)
        {
            reason = null;
            if (inspection.Status == InspectionStatus.Draft)
            {
                reason = "Submit the inspection before requesting the report.";
            }
            else if (inspection.Status == InspectionStatus.ReportQueued)
            {
                reason = "A report is already queued for this inspection.";
            }
            else if (inspection.Status == InspectionStatus.Closed)
            {
                reason = "The inspection is closed.";
            }
            else if (photoCount == 0)
            {
                reason = "The inspection has no photos.";
            }
            else if (string.IsNullOrWhiteSpace(inspection.HandlerEmail))
            {
                reason = "No claim handler e-mail address is set.";
            }
            return reason == null;
        }
    }
}
