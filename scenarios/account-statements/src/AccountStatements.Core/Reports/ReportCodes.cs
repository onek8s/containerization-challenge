using System;
using System.Collections.Generic;
using System.Linq;

namespace Woodgrove.Statements.Core.Reports
{
    public static class ReportCodes
    {
        public const string CustomerStatement = "STATEMENT";
        public const string StatementRun = "STATEMENT-RUN";
        public const string InvoiceCopy = "INVOICE";
        public const string AgedDebt = "AGED-DEBT";
        public const string Portfolio = "PORTFOLIO";
        public const string Transactions = "TRANSACTIONS";
    }

    public enum OutputFormat
    {
        Pdf,
        Excel,
        Word
    }

    public static class OutputFormats
    {
        public static OutputFormat Parse(string text)
        {
            switch ((text ?? "").Trim().ToUpperInvariant())
            {
                case "PDF": return OutputFormat.Pdf;
                case "EXCEL":
                case "XLSX": return OutputFormat.Excel;
                case "WORD":
                case "DOCX": return OutputFormat.Word;
                default: throw new FormatException("Unknown output format '" + text + "'.");
            }
        }

        public static string Extension(OutputFormat format)
        {
            switch (format)
            {
                case OutputFormat.Excel: return ".xlsx";
                case OutputFormat.Word: return ".docx";
                default: return ".pdf";
            }
        }

        public static string MimeType(OutputFormat format)
        {
            switch (format)
            {
                case OutputFormat.Excel: return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case OutputFormat.Word: return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                default: return "application/pdf";
            }
        }

        /// <summary>Render format name understood by the report engine.</summary>
        public static string RenderFormat(OutputFormat format)
        {
            switch (format)
            {
                case OutputFormat.Excel: return "EXCELOPENXML";
                case OutputFormat.Word: return "WORDOPENXML";
                default: return "PDF";
            }
        }
    }

    public class ReportDefinitionInfo
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string ResourceName { get; set; }
        public IList<string> Parameters { get; set; }
        public IList<OutputFormat> Formats { get; set; }
        public bool PerCustomer { get; set; }

        public static readonly IList<ReportDefinitionInfo> All = new List<ReportDefinitionInfo>
        {
            new ReportDefinitionInfo { Code = ReportCodes.CustomerStatement, Title = "Customer account statement", Category = "Customers", ResourceName = "CustomerStatement.rdlc", PerCustomer = true,
                Parameters = new[] { "CustomerNo", "PeriodStart", "StatementDate" }, Formats = new[] { OutputFormat.Pdf, OutputFormat.Excel, OutputFormat.Word } },
            new ReportDefinitionInfo { Code = ReportCodes.StatementRun, Title = "Monthly statement run (all customers)", Category = "Customers", ResourceName = "CustomerStatement.rdlc", PerCustomer = true,
                Parameters = new[] { "PeriodStart", "StatementDate", "MinimumBalance" }, Formats = new[] { OutputFormat.Pdf } },
            new ReportDefinitionInfo { Code = ReportCodes.InvoiceCopy, Title = "Invoice copy", Category = "Customers", ResourceName = "InvoiceCopy.rdlc", PerCustomer = true,
                Parameters = new[] { "InvoiceNo" }, Formats = new[] { OutputFormat.Pdf } },
            new ReportDefinitionInfo { Code = ReportCodes.AgedDebt, Title = "Aged debt by credit controller", Category = "Credit control", ResourceName = "AgedDebt.rdlc",
                Parameters = new[] { "AsOf", "CreditController" }, Formats = new[] { OutputFormat.Pdf, OutputFormat.Excel } },
            new ReportDefinitionInfo { Code = ReportCodes.Portfolio, Title = "Lease portfolio summary", Category = "Management", ResourceName = "PortfolioSummary.rdlc",
                Parameters = new[] { "AsOf" }, Formats = new[] { OutputFormat.Pdf, OutputFormat.Excel } },
            new ReportDefinitionInfo { Code = ReportCodes.Transactions, Title = "Ledger transactions (workbook)", Category = "Credit control", ResourceName = null,
                Parameters = new[] { "From", "To", "CreditController" }, Formats = new[] { OutputFormat.Excel } }
        };

        public static ReportDefinitionInfo Find(string code)
        {
            return All.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }
}
