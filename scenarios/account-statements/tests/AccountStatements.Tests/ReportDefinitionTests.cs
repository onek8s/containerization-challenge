using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Woodgrove.Statements.Core.Statements;
using Woodgrove.Statements.Reporting.Data;
using Xunit;

namespace Woodgrove.Statements.Tests
{
    public class ReportDefinitionTests
    {
        static readonly XNamespace Rdl = "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";

        static XDocument Load(string name)
        {
            return XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Reports", name));
        }

        [Theory]
        [InlineData("CustomerStatement.rdlc", "Company", typeof(CompanyRow))]
        [InlineData("CustomerStatement.rdlc", "Header", typeof(StatementHeaderRow))]
        [InlineData("CustomerStatement.rdlc", "Lines", typeof(StatementLineRow))]
        [InlineData("StatementAging.rdlc", "OpenItems", typeof(OpenItemRow))]
        [InlineData("InvoiceCopy.rdlc", "Company", typeof(CompanyRow))]
        [InlineData("InvoiceCopy.rdlc", "Invoice", typeof(InvoiceHeaderRow))]
        [InlineData("InvoiceCopy.rdlc", "Lines", typeof(InvoiceLineRow))]
        [InlineData("AgedDebt.rdlc", "AgedDebt", typeof(AgedDebtRow))]
        [InlineData("PortfolioSummary.rdlc", "Portfolio", typeof(PortfolioRow))]
        public void Dataset_fields_exist_on_the_row_type(string report, string dataset, Type rowType)
        {
            var fields = Load(report).Descendants(Rdl + "DataSet").Single(d => (string)d.Attribute("Name") == dataset)
                                     .Descendants(Rdl + "DataField").Select(f => f.Value).ToList();
            Assert.NotEmpty(fields);
            foreach (var field in fields)
                Assert.True(rowType.GetProperty(field) != null, report + "/" + dataset + ": " + rowType.Name + " has no property " + field);
        }

        [Theory]
        [InlineData("CustomerStatement.rdlc")]
        [InlineData("StatementAging.rdlc")]
        [InlineData("InvoiceCopy.rdlc")]
        [InlineData("AgedDebt.rdlc")]
        [InlineData("PortfolioSummary.rdlc")]
        public void Names_are_unique_and_datasets_are_used(string report)
        {
            var doc = Load(report);
            var names = doc.Descendants().Where(e => e.Parent?.Name == Rdl + "ReportItems" || e.Name == Rdl + "Textbox" || e.Name == Rdl + "Tablix" || e.Name == Rdl + "DataSet")
                           .Select(e => (string)e.Attribute("Name")).Where(n => n != null).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
            var used = doc.Descendants(Rdl + "DataSetName").Select(e => e.Value).ToList();
            var text = doc.ToString();
            foreach (var ds in doc.Descendants(Rdl + "DataSet").Select(d => (string)d.Attribute("Name")))
                Assert.True(used.Contains(ds) || text.Contains("\"" + ds + "\")"), report + ": dataset " + ds + " is not used");
        }
    }
}
