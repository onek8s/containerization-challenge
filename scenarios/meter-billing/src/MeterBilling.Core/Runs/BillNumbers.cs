using System;
using System.Globalization;

namespace Coho.Billing.Core.Runs
{
    public static class BillNumbers
    {
        /// <summary>Bill number yy + run (5) + sequence (5), e.g. 260048200017.</summary>
        public static string Create(DateTime billDate, int runId, int sequence)
        {
            return billDate.ToString("yy", CultureInfo.InvariantCulture)
                + (runId % 100000).ToString("D5", CultureInfo.InvariantCulture)
                + sequence.ToString("D5", CultureInfo.InvariantCulture);
        }

        /// <summary>Payment reference from the account number and bill number (digits only).</summary>
        public static string PaymentReference(string accountNumber, string billNumber)
        {
            return Output.CreditorReference.Create(accountNumber.Replace("-", string.Empty) + billNumber.Substring(billNumber.Length - 7));
        }
    }
}
