using System;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Tenancies;

namespace Fabrikam.Housing.Rent
{
    public enum MandateStatus
    {
        PendingCustomerApproval = 1,
        PendingSubmission = 2,
        Submitted = 3,
        Active = 4,
        Failed = 5,
        Cancelled = 6,
        Expired = 7
    }

    public enum CollectionFrequency
    {
        Weekly = 1,
        Fortnightly = 2,
        FourWeekly = 3,
        Monthly = 4
    }

    public enum PaymentStatus
    {
        PendingSubmission = 1,
        Submitted = 2,
        Confirmed = 3,
        PaidOut = 4,
        Failed = 5,
        ChargedBack = 6,
        Cancelled = 7
    }

    public class DirectDebitMandate : AuditedEntity
    {
        public int TenancyId { get; set; }
        public Tenancy Tenancy { get; set; }
        /// <summary>Paystream customer and mandate ids.</summary>
        public string ProviderCustomerId { get; set; }
        public string ProviderMandateId { get; set; }
        public string BankReference { get; set; }
        public string AccountHolderName { get; set; }
        public string AccountNumberEnding { get; set; }
        public MandateStatus Status { get; set; }
        public CollectionFrequency Frequency { get; set; }
        /// <summary>Day of month (Monthly) or day of week 1-5 (weekly frequencies).</summary>
        public int CollectionDay { get; set; }
        public decimal Amount { get; set; }
        public DateTime FirstCollectionDate { get; set; }
        public DateTime? NextCollectionDate { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string CancellationReason { get; set; }
    }

    public class DirectDebitPayment : Entity
    {
        public int MandateId { get; set; }
        public DirectDebitMandate Mandate { get; set; }
        public string ProviderPaymentId { get; set; }
        public DateTime ChargeDate { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }
        public string FailureReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? RentTransactionId { get; set; }
    }

    /// <summary>
    /// Works out collection dates. Bacs needs the payment submitted to Paystream at least three working days
    /// before the charge date; monthly collections on the 29th-31st are not allowed.
    /// </summary>
    public class CollectionSchedule
    {
        public const int SubmissionLeadWorkingDays = 3;
        private readonly WorkingCalendar _calendar;

        public CollectionSchedule(WorkingCalendar calendar)
        {
            _calendar = calendar;
        }

        public static void Validate(CollectionFrequency frequency, int day)
        {
            if (frequency == CollectionFrequency.Monthly && (day < 1 || day > 28))
                throw new BusinessRuleException("DD_DAY", "Monthly collections must be on day 1 to 28.");
            if (frequency != CollectionFrequency.Monthly && (day < 1 || day > 5))
                throw new BusinessRuleException("DD_DAY", "Weekly collections must be on a weekday (1 = Monday to 5 = Friday).");
        }

        public DateTime EarliestChargeDate(DateTime today)
        {
            return _calendar.AddWorkingDays(today.Date, SubmissionLeadWorkingDays + 1);
        }

        public DateTime FirstCollection(CollectionFrequency frequency, int day, DateTime today)
        {
            Validate(frequency, day);
            var earliest = EarliestChargeDate(today);
            DateTime candidate;
            if (frequency == CollectionFrequency.Monthly)
            {
                candidate = new DateTime(earliest.Year, earliest.Month, day);
                if (candidate < earliest) candidate = candidate.AddMonths(1);
            }
            else
            {
                candidate = earliest;
                while ((int)candidate.DayOfWeek != day) candidate = candidate.AddDays(1);
            }
            return _calendar.NextWorkingDay(candidate);
        }

        public DateTime Next(DirectDebitMandate mandate, DateTime previous)
        {
            DateTime nominal;
            switch (mandate.Frequency)
            {
                case CollectionFrequency.Weekly: nominal = previous.AddDays(7); break;
                case CollectionFrequency.Fortnightly: nominal = previous.AddDays(14); break;
                case CollectionFrequency.FourWeekly: nominal = previous.AddDays(28); break;
                default:
                    var m = previous.AddMonths(1);
                    nominal = new DateTime(m.Year, m.Month, mandate.CollectionDay);
                    break;
            }
            if (mandate.Frequency != CollectionFrequency.Monthly)
            {
                // Weekly dates are anchored to the weekday, so a bank holiday shift does not drift the schedule.
                while ((int)nominal.DayOfWeek != mandate.CollectionDay) nominal = nominal.AddDays(-1);
            }
            return _calendar.NextWorkingDay(nominal);
        }

        public bool IsDueForSubmission(DateTime chargeDate, DateTime today)
        {
            return _calendar.AddWorkingDays(today.Date, SubmissionLeadWorkingDays) >= chargeDate.Date;
        }

        /// <summary>Amount per collection that clears the weekly charge over the frequency.</summary>
        public static decimal SuggestedAmount(decimal weeklyCharge, CollectionFrequency frequency)
        {
            switch (frequency)
            {
                case CollectionFrequency.Weekly: return weeklyCharge;
                case CollectionFrequency.Fortnightly: return weeklyCharge * 2;
                case CollectionFrequency.FourWeekly: return weeklyCharge * 4;
                default: return Math.Round(weeklyCharge * 52 / 12, 2, MidpointRounding.AwayFromZero);
            }
        }
    }
}
