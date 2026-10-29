using System;
using System.IO;
using System.Reflection;

namespace Woodgrove.Statements.Reporting
{
    /// <summary>Report definitions are compiled into this assembly as embedded resources.</summary>
    public static class ReportDefinitions
    {
        const string Prefix = "Woodgrove.Statements.Reporting.Reports.";

        public const string CustomerStatement = "CustomerStatement.rdlc";
        public const string StatementAging = "StatementAging.rdlc";
        public const string InvoiceCopy = "InvoiceCopy.rdlc";
        public const string AgedDebt = "AgedDebt.rdlc";
        public const string PortfolioSummary = "PortfolioSummary.rdlc";

        public static Stream Open(string name)
        {
            var stream = typeof(ReportDefinitions).Assembly.GetManifestResourceStream(Prefix + name);
            if (stream == null) throw new InvalidOperationException("Report definition " + name + " is not embedded.");
            return stream;
        }

        /// <summary>Report name without extension, as used by subreports and drillthrough actions.</summary>
        public static string ReportName(string fileName)
        {
            return Path.GetFileNameWithoutExtension(fileName);
        }

        public static string[] All()
        {
            return new[] { CustomerStatement, StatementAging, InvoiceCopy, AgedDebt, PortfolioSummary };
        }
    }
}
