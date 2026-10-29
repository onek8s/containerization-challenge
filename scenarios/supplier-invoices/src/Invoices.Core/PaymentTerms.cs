using System;
using System.Globalization;

namespace Wingtip.Invoices.Core
{
    public static class PaymentTerms
    {
        /// <summary>
        /// Due date from ERP payment terms codes: N14, N30, N60 (net days) and EOM30 (end of month + days).
        /// A due date on the invoice is used when it is later than the terms allow.
        /// </summary>
        public static DateTime DueDate(DateTime invoiceDate, string terms, DateTime? invoiceDueDate)
        {
            var calculated = invoiceDate.AddDays(30);
            if (!string.IsNullOrEmpty(terms))
            {
                int days;
                if (terms.StartsWith("EOM", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(terms.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out days))
                {
                    var endOfMonth = new DateTime(invoiceDate.Year, invoiceDate.Month, 1).AddMonths(1).AddDays(-1);
                    calculated = endOfMonth.AddDays(days);
                }
                else if (terms.StartsWith("N", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(terms.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out days))
                {
                    calculated = invoiceDate.AddDays(days);
                }
            }

            if (invoiceDueDate.HasValue && invoiceDueDate.Value > calculated) return invoiceDueDate.Value.Date;
            return calculated.Date;
        }
    }
}
