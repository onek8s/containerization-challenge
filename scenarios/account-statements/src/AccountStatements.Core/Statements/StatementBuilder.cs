using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Woodgrove.Statements.Core.Ledger;

namespace Woodgrove.Statements.Core.Statements
{
    public static class AgingBucketNames
    {
        public const string NotDue = "NOTDUE";
        public const string Days1To30 = "1-30";
        public const string Days31To60 = "31-60";
        public const string Days61To90 = "61-90";
        public const string Over90 = "90+";
        public const string Unallocated = "UNALLOC";

        public static string ForDays(int daysOverdue)
        {
            if (daysOverdue <= 0) return NotDue;
            if (daysOverdue <= 30) return Days1To30;
            if (daysOverdue <= 60) return Days31To60;
            if (daysOverdue <= 90) return Days61To90;
            return Over90;
        }
    }

    /// <summary>
    /// Builds a customer statement from the AR ledger. The ledger only carries the open amount at load time and the
    /// date the item was closed, so the open amount at an earlier statement date is the full amount when the item
    /// was closed after that date (partial payments before the statement date are not split).
    /// </summary>
    public class StatementBuilder
    {
        public int Level1Days { get; set; } = 14;
        public int Level2Days { get; set; } = 45;
        public int Level3Days { get; set; } = 75;

        public Statement Build(Customer customer, IEnumerable<LedgerEntry> entries, DateTime periodStart, DateTime statementDate)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            periodStart = periodStart.Date;
            statementDate = statementDate.Date;
            if (periodStart > statementDate) throw new ArgumentException("Period start is after the statement date.");

            var all = entries.Where(e => e.CustomerId == customer.CustomerId && e.PostingDate.Date <= statementDate).ToList();
            var statement = new Statement
            {
                Customer = customer,
                PeriodStart = periodStart,
                StatementDate = statementDate,
                StatementNo = StatementNumber(customer.CustomerNo, statementDate)
            };

            statement.OpeningBalance = all.Where(e => e.PostingDate.Date < periodStart).Sum(e => e.Amount);

            var balance = statement.OpeningBalance;
            foreach (var e in all.Where(e => e.PostingDate.Date >= periodStart)
                                 .OrderBy(e => e.PostingDate.Date)
                                 .ThenBy(e => EntryTypes.IsDebit(e.EntryType) ? 0 : 1)
                                 .ThenBy(e => e.DocumentNo, StringComparer.Ordinal))
            {
                balance += e.Amount;
                statement.Lines.Add(new StatementLine
                {
                    PostingDate = e.PostingDate.Date,
                    EntryType = e.EntryType,
                    DocumentNo = e.DocumentNo,
                    ContractNo = e.ContractNo,
                    Description = e.Description,
                    DueDate = e.DueDate,
                    Debit = e.Amount > 0 ? e.Amount : 0m,
                    Credit = e.Amount < 0 ? -e.Amount : 0m,
                    Balance = balance
                });
            }
            statement.TotalDebits = statement.Lines.Sum(l => l.Debit);
            statement.TotalCredits = statement.Lines.Sum(l => l.Credit);
            statement.ClosingBalance = balance;

            foreach (var e in all)
            {
                var open = OpenAmountAt(e, statementDate);
                if (open == 0m) continue;
                var due = (e.DueDate ?? e.PostingDate).Date;
                var days = open > 0 ? (statementDate - due).Days : 0;
                var item = new OpenItem
                {
                    EntryType = e.EntryType,
                    DocumentNo = e.DocumentNo,
                    ContractNo = e.ContractNo,
                    PostingDate = e.PostingDate.Date,
                    DueDate = due,
                    OriginalAmount = e.Amount,
                    OpenAmount = open,
                    DaysOverdue = Math.Max(0, days),
                    Bucket = open > 0 ? AgingBucketNames.ForDays(days) : AgingBucketNames.Unallocated,
                    PaymentReference = e.PaymentReference
                };
                statement.OpenItems.Add(item);
                AddToBucket(statement.Aging, item);
            }
            statement.OpenItems = statement.OpenItems.OrderBy(i => i.DueDate).ThenBy(i => i.DocumentNo, StringComparer.Ordinal).ToList();
            statement.DunningLevel = DunningLevel(statement.OpenItems, customer);
            return statement;
        }

        public static decimal OpenAmountAt(LedgerEntry entry, DateTime asOf)
        {
            if (entry.PostingDate.Date > asOf.Date) return 0m;
            if (entry.ClosedDate.HasValue)
                return entry.ClosedDate.Value.Date > asOf.Date ? entry.Amount : 0m;
            return entry.OpenAmount;
        }

        public int DunningLevel(IEnumerable<OpenItem> items, Customer customer)
        {
            var overdue = items.Where(i => i.OpenAmount > 0 && i.DaysOverdue > 0).ToList();
            if (overdue.Count == 0) return 0;
            var maxDays = overdue.Max(i => i.DaysOverdue);
            var level = maxDays > Level3Days ? 3 : maxDays > Level2Days ? 2 : maxDays > Level1Days ? 1 : 0;
            if (customer != null && customer.CreditLimit > 0 && items.Sum(i => i.OpenAmount) > customer.CreditLimit)
                level = Math.Max(level, 2);
            return level;
        }

        public static string StatementNumber(string customerNo, DateTime statementDate)
        {
            return string.Format(CultureInfo.InvariantCulture, "ST{0:yyyyMM}-{1}", statementDate, customerNo);
        }

        static void AddToBucket(AgingBuckets aging, OpenItem item)
        {
            switch (item.Bucket)
            {
                case AgingBucketNames.NotDue: aging.NotDue += item.OpenAmount; break;
                case AgingBucketNames.Days1To30: aging.Days1To30 += item.OpenAmount; break;
                case AgingBucketNames.Days31To60: aging.Days31To60 += item.OpenAmount; break;
                case AgingBucketNames.Days61To90: aging.Days61To90 += item.OpenAmount; break;
                case AgingBucketNames.Over90: aging.Over90 += item.OpenAmount; break;
                default: aging.Unallocated += item.OpenAmount; break;
            }
        }
    }
}
