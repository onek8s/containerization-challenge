using System;

namespace Coho.Billing.Core.Runs
{
    public enum BillRunStage
    {
        Created = 0,
        Rating = 1,
        Review = 2,
        Approved = 3,
        Releasing = 4,
        Completed = 5,
        Held = 8,
        Failed = 9,
        Cancelled = 10
    }

    public enum ExceptionResolution
    {
        Open = 0,

        /// <summary>The read is correct (confirmed by the customer or a site visit), bill as rated.</summary>
        AcceptRead = 1,

        /// <summary>Discard the read and bill on an estimate.</summary>
        UseEstimate = 2,

        /// <summary>Leave the account out of this run; it is billed in the next cycle.</summary>
        Exclude = 3
    }

    public class BillRunSummary
    {
        public int RunId { get; set; }
        public string CycleCode { get; set; }
        public DateTime CutOffDate { get; set; }
        public DateTime BillDate { get; set; }
        public BillRunStage Stage { get; set; }
        public int Accounts { get; set; }
        public int Bills { get; set; }
        public int EstimatedBills { get; set; }
        public int OpenExceptions { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string HeldBy { get; set; }
        public string Message { get; set; }
    }

    public static class BillRunRules
    {
        public static bool CanHold(BillRunStage stage)
        {
            return stage == BillRunStage.Created || stage == BillRunStage.Review || stage == BillRunStage.Approved;
        }

        public static bool CanResume(BillRunStage stage)
        {
            return stage == BillRunStage.Held || stage == BillRunStage.Failed;
        }

        public static bool CanRerate(BillRunStage stage)
        {
            return stage == BillRunStage.Review || stage == BillRunStage.Held || stage == BillRunStage.Failed;
        }

        public static bool CanCancel(BillRunStage stage)
        {
            return stage != BillRunStage.Completed && stage != BillRunStage.Releasing && stage != BillRunStage.Cancelled;
        }

        /// <summary>
        /// A run in review moves on once every exception is resolved. Runs with no exceptions are
        /// approved automatically when the total stays under the auto-release limit.
        /// </summary>
        public static BillRunStage AfterReview(BillRunSummary run, decimal autoReleaseLimit, bool operatorApproved)
        {
            if (run.Stage != BillRunStage.Review)
            {
                return run.Stage;
            }

            if (run.OpenExceptions > 0)
            {
                return BillRunStage.Review;
            }

            if (operatorApproved || run.TotalAmount <= autoReleaseLimit)
            {
                return BillRunStage.Approved;
            }

            return BillRunStage.Review;
        }
    }
}
