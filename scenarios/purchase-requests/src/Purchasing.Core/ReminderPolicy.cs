using System;

namespace Contoso.Purchasing.Core
{
    public class ReminderPolicy
    {
        public ReminderPolicy(int firstReminderAfterDays, int repeatAfterDays)
        {
            FirstReminderAfterDays = firstReminderAfterDays;
            RepeatAfterDays = repeatAfterDays;
        }

        public int FirstReminderAfterDays { get; private set; }
        public int RepeatAfterDays { get; private set; }

        public bool IsDue(PurchaseRequest request, DateTime now)
        {
            if (request.Status != RequestStatus.InApproval) return false;

            var waitingSince = request.LastActivityAt;
            if (waitingSince == null) return false;

            if (BusinessCalendar.BusinessDaysBetween(waitingSince.Value, now) < FirstReminderAfterDays) return false;

            if (request.LastReminderAt != null && request.LastReminderAt > waitingSince
                && BusinessCalendar.BusinessDaysBetween(request.LastReminderAt.Value, now) < RepeatAfterDays)
            {
                return false;
            }

            return true;
        }
    }
}
