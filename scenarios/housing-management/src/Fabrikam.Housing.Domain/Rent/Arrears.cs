using System;
using System.Collections.Generic;
using System.Linq;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Tenancies;

namespace Fabrikam.Housing.Rent
{
    public enum ArrearsStage
    {
        None = 0,
        Reminder = 1,
        FirstLetter = 2,
        SecondLetter = 3,
        NoticeOfSeekingPossession = 4,
        CourtReferral = 5
    }

    public enum ArrearsAction
    {
        None = 0,
        SendSms = 1,
        SendLetter = 2,
        OfficerReview = 3,
        Hold = 4
    }

    public enum ArrangementStatus
    {
        Active = 1,
        Broken = 2,
        Completed = 3,
        Cancelled = 4
    }

    public class PaymentArrangement : AuditedEntity
    {
        public int TenancyId { get; set; }
        public Tenancy Tenancy { get; set; }
        public decimal WeeklyAmountTowardsArrears { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public ArrangementStatus Status { get; set; }
        public string Notes { get; set; }
    }

    public class ArrearsCase : AuditedEntity
    {
        public int TenancyId { get; set; }
        public Tenancy Tenancy { get; set; }
        public ArrearsStage Stage { get; set; }
        public decimal BalanceAtStage { get; set; }
        public DateTime StageReachedOn { get; set; }
        public DateTime? NextReviewOn { get; set; }
        public bool OnHold { get; set; }
        public string HoldReason { get; set; }
        public DateTime? ClosedOn { get; set; }
        public string AssignedTo { get; set; }
        public List<ArrearsCaseNote> Notes { get; set; } = new List<ArrearsCaseNote>();
    }

    public class ArrearsCaseNote : Entity
    {
        public int ArrearsCaseId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string Text { get; set; }
    }

    public class ArrearsDecision
    {
        public ArrearsStage Stage { get; set; }
        public ArrearsAction Action { get; set; }
        public string Reason { get; set; }
        public decimal WeeksInArrears { get; set; }
    }

    /// <summary>
    /// Income team arrears policy IN-04 (rev. 2020). Stages are based on the balance in weeks of rent and
    /// whether the last payment was recent; each stage must have been in place for 14 days before escalation.
    /// A kept arrangement, a pending housing benefit or Universal Credit claim, or a vulnerable tenant puts the
    /// case on hold for officer review instead of escalating automatically.
    /// </summary>
    public class ArrearsPolicy
    {
        public int MinimumDaysAtStage { get; set; } = 14;
        public decimal ReminderWeeks { get; set; } = 1m;
        public decimal FirstLetterWeeks { get; set; } = 2m;
        public decimal SecondLetterWeeks { get; set; } = 4m;
        public decimal NoticeWeeks { get; set; } = 8m;
        public decimal MinimumBalance { get; set; } = 10m;

        public ArrearsDecision Evaluate(Tenancy tenancy, decimal balance, IEnumerable<RentTransaction> recent, ArrearsCase current,
            PaymentArrangement arrangement, DateTime today)
        {
            var weekly = tenancy.WeeklyCharge <= 0 ? 1m : tenancy.WeeklyCharge;
            var weeks = Math.Round(balance / weekly, 1);
            var decision = new ArrearsDecision { WeeksInArrears = weeks, Stage = current?.Stage ?? ArrearsStage.None };

            if (balance < MinimumBalance)
            {
                decision.Stage = ArrearsStage.None;
                decision.Reason = "Balance below threshold";
                return decision;
            }

            var target = weeks >= NoticeWeeks ? ArrearsStage.NoticeOfSeekingPossession
                : weeks >= SecondLetterWeeks ? ArrearsStage.SecondLetter
                : weeks >= FirstLetterWeeks ? ArrearsStage.FirstLetter
                : weeks >= ReminderWeeks ? ArrearsStage.Reminder
                : ArrearsStage.None;

            if (current != null && current.OnHold)
            {
                decision.Action = ArrearsAction.Hold;
                decision.Reason = "Case on hold: " + current.HoldReason;
                return decision;
            }

            if (arrangement != null && arrangement.Status == ArrangementStatus.Active && IsArrangementKept(arrangement, tenancy, recent, today))
            {
                decision.Action = ArrearsAction.Hold;
                decision.Reason = "Payment arrangement is being kept";
                return decision;
            }

            if (target <= decision.Stage)
            {
                decision.Reason = "No escalation needed";
                return decision;
            }

            if (current != null && current.Stage > ArrearsStage.None && (today - current.StageReachedOn).TotalDays < MinimumDaysAtStage)
            {
                decision.Reason = "Current stage reached less than " + MinimumDaysAtStage + " days ago";
                return decision;
            }

            // Escalate one stage at a time.
            var next = (ArrearsStage)Math.Min((int)target, (int)decision.Stage + 1);
            decision.Stage = next;

            var lead = tenancy.Tenants.FirstOrDefault(t => t.IsLeadTenant);
            if (tenancy.HousingBenefitClaimed || tenancy.UniversalCredit || (lead != null && lead.Vulnerable))
            {
                if (next >= ArrearsStage.SecondLetter)
                {
                    decision.Action = ArrearsAction.OfficerReview;
                    decision.Reason = "Benefit claim or vulnerability: officer to review before " + next;
                    return decision;
                }
            }

            switch (next)
            {
                case ArrearsStage.Reminder:
                    decision.Action = ArrearsAction.SendSms;
                    decision.Reason = "Balance over " + ReminderWeeks + " week(s) of rent";
                    break;
                case ArrearsStage.FirstLetter:
                case ArrearsStage.SecondLetter:
                    decision.Action = ArrearsAction.SendLetter;
                    decision.Reason = "Balance " + weeks + " weeks of rent";
                    break;
                default:
                    decision.Action = ArrearsAction.OfficerReview;
                    decision.Reason = "Notice of Seeking Possession must be served by the housing officer";
                    break;
            }
            return decision;
        }

        public static bool IsArrangementKept(PaymentArrangement arrangement, Tenancy tenancy, IEnumerable<RentTransaction> recent, DateTime today)
        {
            var from = today.AddDays(-28) < arrangement.StartDate ? arrangement.StartDate : today.AddDays(-28);
            var weeks = Math.Max(1, (decimal)(today - from).TotalDays / 7m);
            var paid = -recent.Where(t => RentTransaction.IsPayment(t.Type) && t.PostedOn >= from).Sum(t => t.Amount);
            var expected = (tenancy.WeeklyCharge + arrangement.WeeklyAmountTowardsArrears) * weeks;
            return paid >= expected * 0.95m;
        }
    }
}
