using System;
using Southridge.Clinic.Data;

namespace Southridge.Clinic.Billing
{
    /// <summary>Statement cycle: due date, dunning level and the message printed on the statement.</summary>
    public class StatementPolicy
    {
        public StatementPolicy(int dueDays, int collectionsLevel)
        {
            DueDays = dueDays;
            CollectionsLevel = collectionsLevel;
        }

        public int DueDays { get; private set; }

        public int CollectionsLevel { get; private set; }

        public int NextDunningLevel(StatementData data)
        {
            // The level goes up when the previous statement was not paid down.
            if (data.PreviousAmountDue > 0 && data.Balance >= data.PreviousAmountDue)
            {
                return Math.Min(data.PreviousDunningLevel + 1, CollectionsLevel);
            }

            return 0;
        }

        public string Message(int dunningLevel)
        {
            switch (dunningLevel)
            {
                case 0:
                    return "Thank you for choosing Southridge Medical Group. Please pay the amount due by the due date.";
                case 1:
                    return "Our records show your previous statement is unpaid. Please pay now or call us to set up a payment plan.";
                case 2:
                    return "SECOND NOTICE: Your account is past due. Please contact our billing office within 10 days.";
                default:
                    return "FINAL NOTICE: Your account will be referred to a collection agency if it is not paid within 10 days.";
            }
        }

        public DateTime DueDate(DateTime statementDate)
        {
            return statementDate.AddDays(DueDays);
        }
    }
}
