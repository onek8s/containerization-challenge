using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Rent;

namespace Fabrikam.Housing.Finance
{
    public class JournalBatch : Entity
    {
        public DateTime CreatedAt { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string FileName { get; set; }
        public int LineCount { get; set; }
        public decimal TotalDebit { get; set; }
        public string Status { get; set; }
    }

    public class JournalLine
    {
        public DateTime Date { get; set; }
        public string Nominal { get; set; }
        public string CostCentre { get; set; }
        public string Reference { get; set; }
        public string Narrative { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    /// <summary>
    /// Nominal codes in the Sage 200 chart of accounts used by Fabrikam finance.
    /// </summary>
    public static class Nominals
    {
        public const string RentIncome = "4000";
        public const string ServiceChargeIncome = "4010";
        public const string WaterChargeIncome = "4020";
        public const string RechargeIncome = "4050";
        public const string RentDebtors = "1100";
        public const string BankCurrent = "1200";
        public const string PaystreamClearing = "1210";
        public const string HousingBenefitControl = "1150";
        public const string BadDebtWriteOff = "8100";
        public const string ResponsiveRepairs = "5100";
        public const string VatInput = "2201";
        public const string PurchaseLedgerControl = "2100";

        public static string IncomeFor(TransactionType type)
        {
            switch (type)
            {
                case TransactionType.RentCharge: return RentIncome;
                case TransactionType.ServiceCharge: return ServiceChargeIncome;
                case TransactionType.WaterCharge: return WaterChargeIncome;
                case TransactionType.RechargeRepair: return RechargeIncome;
                case TransactionType.CardPayment:
                case TransactionType.DirectDebit:
                case TransactionType.DirectDebitReturned: return PaystreamClearing;
                case TransactionType.HousingBenefit: return HousingBenefitControl;
                case TransactionType.WriteOff: return BadDebtWriteOff;
                default: return BankCurrent;
            }
        }
    }

    public class JournalBuilder
    {
        /// <summary>
        /// Summarises rent account transactions by date, type and patch into balanced journal lines
        /// against the rent debtors control account.
        /// </summary>
        public IList<JournalLine> FromRentTransactions(IEnumerable<(RentTransaction Tx, string Patch)> items)
        {
            var lines = new List<JournalLine>();
            foreach (var g in items.GroupBy(i => new { i.Tx.PostedOn.Date, i.Tx.Type, i.Patch }).OrderBy(g => g.Key.Date).ThenBy(g => g.Key.Type))
            {
                var total = Money.Round(g.Sum(i => i.Tx.Amount));
                if (total == 0) continue;
                var narrative = $"{g.Key.Type} {g.Count()} lines";
                var reference = "RENT" + g.Key.Date.ToString("yyMMdd", CultureInfo.InvariantCulture);
                var other = Nominals.IncomeFor(g.Key.Type);
                // Debits on the rent account raise the debtor; credits reduce it.
                lines.Add(new JournalLine { Date = g.Key.Date, Nominal = Nominals.RentDebtors, CostCentre = g.Key.Patch, Reference = reference, Narrative = narrative, Debit = total > 0 ? total : 0, Credit = total < 0 ? -total : 0 });
                lines.Add(new JournalLine { Date = g.Key.Date, Nominal = other, CostCentre = g.Key.Patch, Reference = reference, Narrative = narrative, Debit = total < 0 ? -total : 0, Credit = total > 0 ? total : 0 });
            }
            return lines;
        }

        public IList<JournalLine> FromInvoices(IEnumerable<Contractors.ContractorInvoice> invoices, Func<Contractors.ContractorInvoice, string> patchOf)
        {
            var lines = new List<JournalLine>();
            foreach (var i in invoices)
            {
                var date = i.ApprovedAt?.Date ?? i.InvoiceDate.Date;
                var reference = (i.Contractor?.Code ?? "C") + "/" + i.InvoiceNumber;
                lines.Add(new JournalLine { Date = date, Nominal = Nominals.ResponsiveRepairs, CostCentre = patchOf(i), Reference = reference, Narrative = "Repairs " + i.WorkOrder?.Reference, Debit = i.NetAmount });
                if (i.VatAmount != 0)
                    lines.Add(new JournalLine { Date = date, Nominal = Nominals.VatInput, CostCentre = "", Reference = reference, Narrative = "VAT", Debit = i.VatAmount });
                lines.Add(new JournalLine { Date = date, Nominal = Nominals.PurchaseLedgerControl, CostCentre = "", Reference = reference, Narrative = "Contractor invoice", Credit = i.GrossAmount });
            }
            return lines;
        }

        /// <summary>
        /// Sage 200 nominal journal import layout: comma separated, dd/MM/yyyy dates, amounts with two decimals,
        /// header row, Windows line endings.
        /// </summary>
        public static string ToCsv(IEnumerable<JournalLine> lines)
        {
            var uk = CultureInfo.GetCultureInfo("en-GB");
            var sb = new StringBuilder();
            sb.Append("Date,Nominal,CostCentre,Reference,Narrative,Debit,Credit\r\n");
            foreach (var l in lines)
            {
                sb.Append(l.Date.ToString("dd/MM/yyyy", uk)).Append(',')
                  .Append(l.Nominal).Append(',')
                  .Append(l.CostCentre).Append(',')
                  .Append(Quote(l.Reference)).Append(',')
                  .Append(Quote(l.Narrative)).Append(',')
                  .Append(l.Debit.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                  .Append(l.Credit.ToString("0.00", CultureInfo.InvariantCulture)).Append("\r\n");
            }
            return sb.ToString();
        }

        private static string Quote(string s) => s != null && (s.Contains(",") || s.Contains("\"")) ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
