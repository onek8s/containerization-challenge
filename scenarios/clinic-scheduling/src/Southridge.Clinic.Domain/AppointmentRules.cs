using System;
using System.Collections.Generic;

namespace Southridge.Clinic.Domain
{
    /// <summary>Appointment status transitions and the front desk policies (late cancellation, no-show).</summary>
    public static class AppointmentRules
    {
        public const int LateCancelHours = 24;
        public const int NoShowGraceMinutes = 30;
        public const int OnlineBookingMinLeadHours = 2;
        public const int OnlineBookingMaxDaysAhead = 90;

        private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> Allowed = new Dictionary<AppointmentStatus, AppointmentStatus[]>
        {
            { AppointmentStatus.Scheduled, new[] { AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled, AppointmentStatus.NoShow, AppointmentStatus.Rescheduled } },
            { AppointmentStatus.Confirmed, new[] { AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled, AppointmentStatus.NoShow, AppointmentStatus.Rescheduled } },
            { AppointmentStatus.CheckedIn, new[] { AppointmentStatus.InRoom, AppointmentStatus.Completed, AppointmentStatus.Cancelled } },
            { AppointmentStatus.InRoom, new[] { AppointmentStatus.Completed } },
            { AppointmentStatus.Completed, new AppointmentStatus[0] },
            { AppointmentStatus.Cancelled, new AppointmentStatus[0] },
            { AppointmentStatus.NoShow, new[] { AppointmentStatus.CheckedIn } },
            { AppointmentStatus.Rescheduled, new AppointmentStatus[0] }
        };

        public static bool CanChange(AppointmentStatus from, AppointmentStatus to)
        {
            return Array.IndexOf(Allowed[from], to) >= 0;
        }

        public static void EnsureCanChange(AppointmentStatus from, AppointmentStatus to)
        {
            if (!CanChange(from, to))
            {
                throw new ClinicRuleException(string.Format("An appointment that is {0} cannot be changed to {1}.", from, to));
            }
        }

        public static bool IsActive(AppointmentStatus status)
        {
            return status == AppointmentStatus.Scheduled || status == AppointmentStatus.Confirmed
                || status == AppointmentStatus.CheckedIn || status == AppointmentStatus.InRoom;
        }

        public static bool IsLateCancellation(DateTime appointmentStart, DateTime cancelledAt)
        {
            return appointmentStart - cancelledAt < TimeSpan.FromHours(LateCancelHours);
        }

        public static bool IsNoShow(DateTime appointmentStart, AppointmentStatus status, DateTime now)
        {
            return (status == AppointmentStatus.Scheduled || status == AppointmentStatus.Confirmed)
                && now > appointmentStart.AddMinutes(NoShowGraceMinutes);
        }

        public static bool CanBookOnline(DateTime slotStart, DateTime now)
        {
            return slotStart >= now.AddHours(OnlineBookingMinLeadHours) && slotStart.Date <= now.Date.AddDays(OnlineBookingMaxDaysAhead);
        }

        /// <summary>Patients may check in at a kiosk from 45 minutes before until 15 minutes after the start.</summary>
        public static bool IsKioskCheckInWindow(DateTime appointmentStart, DateTime now)
        {
            return now >= appointmentStart.AddMinutes(-45) && now <= appointmentStart.AddMinutes(15);
        }
    }

    [Serializable]
    public class ClinicRuleException : Exception
    {
        public ClinicRuleException()
        {
        }

        public ClinicRuleException(string message)
            : base(message)
        {
        }

        public ClinicRuleException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected ClinicRuleException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
