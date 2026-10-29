using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Payments;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Core.Scheduling;
using Woodgrove.Statements.Core.Statements;
using Woodgrove.Statements.Data;
using Woodgrove.Statements.Reporting.Barcodes;
using Woodgrove.Statements.Reporting.Data;
using Woodgrove.Statements.Reporting.Excel;

namespace Woodgrove.Statements.Reporting
{
    /// <summary>Builds the data for each document type and renders it. Shared by the portal, the document API and the distribution service.</summary>
    public class DocumentService
    {
        readonly CustomerRepository _customers;
        readonly LedgerRepository _ledger;
        readonly DocumentRepository _documents;
        readonly ReportRenderer _renderer;
        readonly StatementBuilder _builder = new StatementBuilder();
        CompanyProfile _company;

        public DocumentService(Db db) : this(new CustomerRepository(db), new LedgerRepository(db), new DocumentRepository(db), new ReportRenderer())
        {
        }

        public DocumentService(CustomerRepository customers, LedgerRepository ledger, DocumentRepository documents, ReportRenderer renderer)
        {
            _customers = customers;
            _ledger = ledger;
            _documents = documents;
            _renderer = renderer;
        }

        public int PaymentTermDays { get; set; } = 14;

        CompanyProfile Company
        {
            get { return _company ?? (_company = _documents.Company()); }
        }

        /// <summary>Renders any catalog report from subscription or request parameters.</summary>
        public RenderedDocument Render(string reportCode, ParameterSet parameters, OutputFormat format, DateTime runDate)
        {
            var info = ReportDefinitionInfo.Find(reportCode);
            if (info == null) throw new ArgumentException("Unknown report " + reportCode + ".");
            if (!info.Formats.Contains(format)) throw new ArgumentException(info.Title + " is not available as " + format + ".");
            switch (info.Code)
            {
                case ReportCodes.CustomerStatement:
                    var customerNo = parameters["CustomerNo"];
                    if (string.IsNullOrWhiteSpace(customerNo)) throw new ArgumentException("CustomerNo is required.");
                    var statementDate = parameters.GetDate("StatementDate", runDate.Date);
                    var periodStart = parameters.GetDate("PeriodStart", new DateTime(statementDate.Year, statementDate.Month, 1));
                    return CustomerStatement(customerNo, periodStart, statementDate, format);
                case ReportCodes.InvoiceCopy:
                    return InvoiceCopy(parameters["InvoiceNo"]);
                case ReportCodes.AgedDebt:
                    return AgedDebt(parameters.GetDate("AsOf", runDate.Date.AddDays(-1)), parameters["CreditController"], format);
                case ReportCodes.Portfolio:
                    return Portfolio(parameters.GetDate("AsOf", runDate.Date), format);
                case ReportCodes.Transactions:
                    return Transactions(parameters.GetDate("From", runDate.Date.AddDays(-1)), parameters.GetDate("To", runDate.Date.AddDays(-1)), parameters["CreditController"]);
                default:
                    throw new ArgumentException(info.Title + " is produced by a statement run, not as a single document.");
            }
        }

        public Statement BuildStatement(Customer customer, DateTime periodStart, DateTime statementDate)
        {
            return _builder.Build(customer, _ledger.ForCustomer(customer.CustomerId, statementDate), periodStart, statementDate);
        }

        public RenderedDocument CustomerStatement(string customerNo, DateTime periodStart, DateTime statementDate, OutputFormat format)
        {
            var customer = _customers.Get(customerNo);
            if (customer == null) throw new KeyNotFoundException("Customer " + customerNo + " not found.");
            return CustomerStatement(BuildStatement(customer, periodStart, statementDate), format);
        }

        public RenderedDocument CustomerStatement(Statement statement, OutputFormat format)
        {
            var job = StatementJob(statement);
            int pages;
            var bytes = _renderer.Render(job, format, out pages);
            return new RenderedDocument
            {
                Content = bytes,
                Format = format,
                PageCount = pages,
                CustomerId = statement.Customer.CustomerId,
                CustomerNo = statement.Customer.CustomerNo,
                FileName = statement.Customer.CustomerNo + "_" + statement.StatementDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + OutputFormats.Extension(format)
            };
        }

        public ReportJob StatementJob(Statement statement)
        {
            var c = statement.Customer;
            var due = Math.Max(0m, statement.Aging.Total);
            var reference = StatementReference(c.CustomerNo);
            var paymentDue = statement.StatementDate.AddDays(PaymentTermDays);
            string virtualCode = null;
            byte[] barcode = null;
            if (due > 0 && c.CurrencyCode == "EUR" && Company.Iban.StartsWith("FI", StringComparison.OrdinalIgnoreCase))
            {
                virtualCode = BankBarcode.Virtual(Company.Iban, due, reference, paymentDue);
                barcode = BarcodeImage.Png(virtualCode);
            }
            var header = new StatementHeaderRow
            {
                CustomerNo = c.CustomerNo,
                CustomerName = c.Name,
                AddressBlock = AddressFormatter.Block(c),
                Language = c.Language,
                CurrencyCode = c.CurrencyCode,
                StatementNo = statement.StatementNo,
                PeriodStart = statement.PeriodStart,
                StatementDate = statement.StatementDate,
                OpeningBalance = statement.OpeningBalance,
                ClosingBalance = statement.ClosingBalance,
                TotalDebits = statement.TotalDebits,
                TotalCredits = statement.TotalCredits,
                OverdueAmount = statement.Aging.Overdue,
                DueAmount = due,
                PaymentDueDate = paymentDue,
                PaymentReference = reference,
                VirtualBarcode = virtualCode,
                BarcodeImage = barcode,
                DunningLevel = statement.DunningLevel,
                CreditController = c.CreditController
            };
            var job = new ReportJob { Definition = ReportDefinitions.CustomerStatement };
            job.DataSources["Company"] = new[] { CompanyRow.From(Company) };
            job.DataSources["Header"] = new[] { header };
            job.DataSources["Lines"] = StatementLineRow.From(statement.Lines);
            job.Parameters["Language"] = string.IsNullOrEmpty(c.Language) ? "en-GB" : c.Language;
            job.Subreports["StatementAging"] = ReportDefinitions.StatementAging;
            var openItems = OpenItemRow.From(statement.OpenItems);
            job.SubreportData = (report, dataset) => dataset == "OpenItems" ? openItems : null;
            return job;
        }

        /// <summary>Customer level reference for statement payments: 9 + customer number digits + check digit.</summary>
        public static string StatementReference(string customerNo)
        {
            var digits = new string((customerNo ?? "").Where(char.IsDigit).ToArray());
            var body = "9" + digits;
            return body + BankBarcode.CheckDigit(body).ToString(CultureInfo.InvariantCulture);
        }

        public RenderedDocument InvoiceCopy(string invoiceNo)
        {
            Customer customer;
            Invoice invoice;
            var job = InvoiceJob(invoiceNo, out customer, out invoice);
            int pages;
            var bytes = _renderer.Render(job, OutputFormat.Pdf, out pages);
            return new RenderedDocument
            {
                Content = bytes, Format = OutputFormat.Pdf, PageCount = pages, CustomerId = customer.CustomerId, CustomerNo = customer.CustomerNo,
                FileName = "Invoice_" + invoice.InvoiceNo + ".pdf"
            };
        }

        public ReportJob InvoiceJob(string invoiceNo, out Customer customer, out Invoice invoice)
        {
            if (string.IsNullOrWhiteSpace(invoiceNo)) throw new ArgumentException("InvoiceNo is required.");
            invoice = _documents.Invoice(invoiceNo.Trim());
            if (invoice == null) throw new KeyNotFoundException("Invoice " + invoiceNo + " not found.");
            customer = _customers.GetById(invoice.CustomerId);
            var contractNo = invoice.ContractNo;
            var contract = string.IsNullOrEmpty(contractNo) ? null
                : _documents.ContractsForCustomer(customer.CustomerId).FirstOrDefault(x => x.ContractNo == contractNo);
            string virtualCode = null;
            byte[] barcode = null;
            if (invoice.CurrencyCode == "EUR" && Company.Iban.StartsWith("FI", StringComparison.OrdinalIgnoreCase))
            {
                virtualCode = BankBarcode.Virtual(Company.Iban, invoice.TotalAmount, invoice.PaymentReference, invoice.DueDate);
                barcode = BarcodeImage.Png(virtualCode);
            }
            var header = new InvoiceHeaderRow
            {
                InvoiceNo = invoice.InvoiceNo,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                PeriodFrom = invoice.PeriodFrom,
                PeriodTo = invoice.PeriodTo,
                CustomerNo = customer.CustomerNo,
                CustomerName = customer.Name,
                AddressBlock = AddressFormatter.Block(customer),
                ContractNo = invoice.ContractNo,
                AssetDescription = contract?.AssetDescription,
                CustomerReference = invoice.CustomerReference,
                NetAmount = invoice.NetAmount,
                VatAmount = invoice.VatAmount,
                TotalAmount = invoice.TotalAmount,
                PaymentReference = invoice.PaymentReference,
                CurrencyCode = invoice.CurrencyCode,
                VirtualBarcode = virtualCode,
                BarcodeImage = barcode,
                ReverseCharge = customer.CountryCode != "FI" && invoice.VatAmount == 0m,
                IsCopy = true
            };
            var job = new ReportJob { Definition = ReportDefinitions.InvoiceCopy };
            job.DataSources["Company"] = new[] { CompanyRow.From(Company) };
            job.DataSources["Invoice"] = new[] { header };
            job.DataSources["Lines"] = InvoiceLineRow.From(invoice.Lines);
            job.Parameters["Language"] = string.IsNullOrEmpty(customer.Language) ? "en-GB" : customer.Language;
            return job;
        }

        public IList<AgedDebtRow> AgedDebtRows(DateTime asOf, string creditController)
        {
            var customers = _customers.All().Where(c => string.IsNullOrWhiteSpace(creditController) ||
                                                        string.Equals(c.CreditController, creditController, StringComparison.OrdinalIgnoreCase));
            return Core.Statements.AgedDebt.Build(customers, _ledger.All(asOf), asOf);
        }

        public RenderedDocument AgedDebt(DateTime asOf, string creditController, OutputFormat format)
        {
            var rows = AgedDebtRows(asOf, creditController);
            var name = "AgedDebt_" + asOf.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (format == OutputFormat.Excel)
                return new RenderedDocument { Content = Workbooks.AgedDebt(rows, asOf, creditController, Company.CompanyName), Format = format, FileName = name + ".xlsx" };

            var job = AgedDebtJob(rows, asOf, creditController);
            int pages;
            return new RenderedDocument { Content = _renderer.Render(job, format, out pages), Format = format, PageCount = pages, FileName = name + OutputFormats.Extension(format) };
        }

        public ReportJob AgedDebtJob(IList<AgedDebtRow> rows, DateTime asOf, string creditController)
        {
            var job = new ReportJob { Definition = ReportDefinitions.AgedDebt };
            job.DataSources["Company"] = new[] { CompanyRow.From(Company) };
            job.DataSources["AgedDebt"] = rows;
            job.Parameters["AsOf"] = asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            job.Parameters["CreditController"] = string.IsNullOrWhiteSpace(creditController) ? null : creditController;
            job.Parameters["Language"] = "en-GB";
            return job;
        }

        public IList<PortfolioRow> PortfolioRows(DateTime asOf)
        {
            var customers = _customers.All().ToDictionary(c => c.CustomerId);
            return Core.Statements.Portfolio.Build(_documents.Contracts(), customers, asOf);
        }

        public RenderedDocument Portfolio(DateTime asOf, OutputFormat format)
        {
            var rows = PortfolioRows(asOf);
            var name = "Portfolio_" + asOf.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (format == OutputFormat.Excel)
                return new RenderedDocument { Content = Workbooks.Portfolio(rows, asOf, Company.CompanyName), Format = format, FileName = name + ".xlsx" };

            var job = PortfolioJob(rows, asOf);
            int pages;
            return new RenderedDocument { Content = _renderer.Render(job, format, out pages), Format = format, PageCount = pages, FileName = name + OutputFormats.Extension(format) };
        }

        public ReportJob PortfolioJob(IList<PortfolioRow> rows, DateTime asOf)
        {
            var job = new ReportJob { Definition = ReportDefinitions.PortfolioSummary };
            job.DataSources["Company"] = new[] { CompanyRow.From(Company) };
            job.DataSources["Portfolio"] = rows;
            job.Parameters["AsOf"] = asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            job.Parameters["Language"] = "en-GB";
            return job;
        }

        public RenderedDocument Transactions(DateTime from, DateTime to, string creditController)
        {
            if (to < from) throw new ArgumentException("To is before From.");
            var rows = _ledger.Transactions(from, to, creditController);
            return new RenderedDocument
            {
                Content = Workbooks.Transactions(rows, from, to, Company.CompanyName),
                Format = OutputFormat.Excel,
                FileName = "AR_Transactions_" + from.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + (to > from ? "-" + to.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : "") + ".xlsx"
            };
        }
    }
}
