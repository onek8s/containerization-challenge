using System;
using System.Collections.Generic;

namespace CreditDesk.Core
{
    /// <summary>
    /// Credit policy CP-04 (rev. 2021). Thresholds are configured per site in the
    /// credit service configuration; defaults match the policy document.
    /// </summary>
    public class ApprovalPolicy
    {
        public decimal AutomaticIncreaseLimit { get; set; } = 2500m;
        public decimal ControllerLimit { get; set; } = 50000m;
        public decimal FinanceManagerLimit { get; set; } = 250000m;
        public decimal MaximumMultiplier { get; set; } = 3m;
        public int MaxOverdueDays { get; set; } = 60;
        public int MinimumCustomerMonths { get; set; } = 6;
        public int ApprovalValidityDays { get; set; } = 365;

        public ApprovalLevel RequiredLevel(decimal currentLimit, decimal requestedLimit)
        {
            if (requestedLimit <= currentLimit)
                return ApprovalLevel.Automatic;

            var increase = requestedLimit - currentLimit;
            if (increase <= AutomaticIncreaseLimit)
                return ApprovalLevel.Automatic;
            if (requestedLimit <= ControllerLimit)
                return ApprovalLevel.CreditController;
            if (requestedLimit <= FinanceManagerLimit)
                return ApprovalLevel.FinanceManager;
            return ApprovalLevel.CreditCommittee;
        }

        /// <summary>Checks a new request; returns the reasons it cannot be submitted.</summary>
        public IList<string> Validate(CustomerAccount account, decimal requestedLimit, string reason, DateTime today)
        {
            var errors = new List<string>();
            if (account == null)
            {
                errors.Add("Customer account not found.");
                return errors;
            }

            if (requestedLimit < 0)
                errors.Add("Credit limit cannot be negative.");
            if (requestedLimit % 100 != 0)
                errors.Add("Credit limits are set in steps of 100.");
            if (requestedLimit == account.CreditLimit)
                errors.Add("Requested limit is the same as the current limit.");
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
                errors.Add("Give a reason of at least 10 characters.");

            if (requestedLimit > account.CreditLimit)
            {
                if (account.OnStop)
                    errors.Add("Account is on stop. Clear the stop before requesting an increase.");
                if (account.OldestOverdueDays > MaxOverdueDays)
                    errors.Add($"Account has invoices more than {MaxOverdueDays} days overdue.");
                if (account.CreditLimit > 0 && requestedLimit > account.CreditLimit * MaximumMultiplier)
                    errors.Add($"Increase is more than {MaximumMultiplier:0.#} times the current limit. Split the request or raise it with the credit committee.");
                if (account.CustomerSince.HasValue && MonthsBetween(account.CustomerSince.Value, today) < MinimumCustomerMonths
                    && requestedLimit > ControllerLimit)
                    errors.Add($"Customers younger than {MinimumCustomerMonths} months cannot exceed {ControllerLimit:N0}.");
            }

            return errors;
        }

        /// <summary>Whether the given approver can decide a request at the given level.</summary>
        public static bool CanDecide(ApprovalLevel required, ApproverRoles roles)
        {
            switch (required)
            {
                case ApprovalLevel.Automatic:
                case ApprovalLevel.CreditController:
                    return (roles & (ApproverRoles.CreditController | ApproverRoles.FinanceManager | ApproverRoles.CreditCommittee)) != 0;
                case ApprovalLevel.FinanceManager:
                    return (roles & (ApproverRoles.FinanceManager | ApproverRoles.CreditCommittee)) != 0;
                case ApprovalLevel.CreditCommittee:
                    return (roles & ApproverRoles.CreditCommittee) != 0;
                default:
                    return false;
            }
        }

        public DateTime ValidUntil(DateTime decidedAt) => decidedAt.Date.AddDays(ApprovalValidityDays);

        private static int MonthsBetween(DateTime from, DateTime to)
        {
            var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
            if (to.Day < from.Day) months--;
            return months;
        }
    }

    [Flags]
    public enum ApproverRoles
    {
        None = 0,
        SalesRep = 1,
        CreditController = 2,
        FinanceManager = 4,
        CreditCommittee = 8
    }
}
