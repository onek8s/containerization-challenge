using System;
using System.Collections.Generic;
using System.Linq;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Contractors;

namespace Fabrikam.Housing.Repairs
{
    /// <summary>
    /// Response times from the Fabrikam repairs policy RP-01: emergencies attended within 24 hours (made safe),
    /// urgent repairs in 5 working days, routine in 20 working days and planned work within 90 calendar days.
    /// </summary>
    public class RepairRules
    {
        private readonly WorkingCalendar _calendar;

        public RepairRules(WorkingCalendar calendar)
        {
            _calendar = calendar;
        }

        public DateTime TargetDate(RepairPriority priority, DateTime reportedAt)
        {
            switch (priority)
            {
                case RepairPriority.Emergency: return reportedAt.AddHours(24);
                case RepairPriority.Urgent: return EndOfDay(_calendar.AddWorkingDays(reportedAt, 5));
                case RepairPriority.Routine: return EndOfDay(_calendar.AddWorkingDays(reportedAt, 20));
                default: return EndOfDay(reportedAt.AddDays(90));
            }
        }

        private static DateTime EndOfDay(DateTime d) => d.Date.AddHours(17).AddMinutes(30);

        public static bool IsBreached(WorkOrder order, DateTime now)
        {
            var end = order.CompletedAt ?? now;
            return end > order.TargetDate && order.Status != WorkOrderStatus.Cancelled;
        }

        private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> Allowed = new Dictionary<WorkOrderStatus, WorkOrderStatus[]>
        {
            [WorkOrderStatus.Reported] = new[] { WorkOrderStatus.Triaged, WorkOrderStatus.Assigned, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.Triaged] = new[] { WorkOrderStatus.Assigned, WorkOrderStatus.OnHold, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.Assigned] = new[] { WorkOrderStatus.Accepted, WorkOrderStatus.Triaged, WorkOrderStatus.Appointed, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.Accepted] = new[] { WorkOrderStatus.Appointed, WorkOrderStatus.InProgress, WorkOrderStatus.Completed, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.Appointed] = new[] { WorkOrderStatus.Appointed, WorkOrderStatus.InProgress, WorkOrderStatus.NoAccess, WorkOrderStatus.Completed, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.InProgress] = new[] { WorkOrderStatus.Completed, WorkOrderStatus.OnHold, WorkOrderStatus.Appointed },
            [WorkOrderStatus.NoAccess] = new[] { WorkOrderStatus.Appointed, WorkOrderStatus.Cancelled, WorkOrderStatus.OnHold },
            [WorkOrderStatus.OnHold] = new[] { WorkOrderStatus.Triaged, WorkOrderStatus.Assigned, WorkOrderStatus.Appointed, WorkOrderStatus.Cancelled },
            [WorkOrderStatus.Completed] = new WorkOrderStatus[0],
            [WorkOrderStatus.Cancelled] = new WorkOrderStatus[0]
        };

        public static bool CanMove(WorkOrderStatus from, WorkOrderStatus to) => Allowed[from].Contains(to);

        public static void Move(WorkOrder order, WorkOrderStatus to, string actor, DateTime at, string text = null)
        {
            if (!CanMove(order.Status, to))
                throw new BusinessRuleException("WO_STATUS", $"Work order {order.Reference} cannot move from {order.Status} to {to}.");
            order.Status = to;
            order.ModifiedAt = at;
            order.ModifiedBy = actor;
            order.Events.Add(new WorkOrderEvent { At = at, Actor = actor, NewStatus = to, Text = text });
        }

        /// <summary>
        /// Picks the contractor for a job: one that covers the trade and the patch, with the lowest number of
        /// open jobs relative to its weekly capacity. Emergency work only goes to contractors with an out of hours service.
        /// </summary>
        public static Contractor ChooseContractor(IEnumerable<Contractor> contractors, Trade trade, string patchCode,
            RepairPriority priority, IDictionary<int, int> openJobs)
        {
            return contractors
                .Where(c => c.Active && c.CoversTrade(trade) && c.CoversPatch(patchCode))
                .Where(c => priority != RepairPriority.Emergency || c.OutOfHours)
                .OrderBy(c => (openJobs.TryGetValue(c.Id, out var n) ? n : 0) / (double)Math.Max(1, c.WeeklyCapacity))
                .ThenBy(c => c.Code)
                .FirstOrDefault();
        }

        public static string NextReference(int year, int sequence) => $"R{year % 100:00}-{sequence:000000}";

        /// <summary>
        /// Tenants may choose an appointment from the second working day after reporting (routine) or the next
        /// working day (urgent). Morning 08:00-12:00, afternoon 12:00-17:00, school run 09:30-14:30.
        /// </summary>
        public IEnumerable<DateTime> AppointmentDays(RepairPriority priority, DateTime reportedAt, int count)
        {
            var first = priority == RepairPriority.Urgent ? 1 : 2;
            var d = _calendar.AddWorkingDays(reportedAt.Date, first);
            for (var i = 0; i < count; i++)
            {
                yield return d;
                d = _calendar.AddWorkingDays(d, 1);
            }
        }
    }
}
