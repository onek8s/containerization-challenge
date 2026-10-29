using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Model;

namespace Woodgrove.Statements.Data
{
    public class DocumentRepository
    {
        readonly Db _db;

        public DocumentRepository(Db db) { _db = db; }

        public CompanyProfile Company()
        {
            using (var c = _db.Open())
                return c.QuerySingle<CompanyProfile>(@"SELECT CompanyName, AddressLine1, PostalCode, City, BusinessId, VatNumber, Phone, Email, Web,
                                                              BankName, Iban, Bic, Logo FROM stm.CompanyProfile WHERE ProfileId = 1");
        }

        public Invoice Invoice(string invoiceNo)
        {
            using (var c = _db.Open())
            {
                var invoice = c.QuerySingleOrDefault<Invoice>(@"
SELECT InvoiceId, InvoiceNo, CustomerId, ContractNo, InvoiceDate, DueDate, PeriodFrom, PeriodTo, NetAmount, VatAmount, TotalAmount,
       PaymentReference, CurrencyCode, CustomerReference
  FROM stm.Invoice WHERE InvoiceNo = @invoiceNo", new { invoiceNo });
                if (invoice != null)
                    invoice.Lines = c.Query<InvoiceLine>(@"
SELECT InvoiceId, LineNumber, Description, Quantity, UnitPrice, VatRate, NetAmount, VatAmount
  FROM stm.InvoiceLine WHERE InvoiceId = @id ORDER BY LineNumber", new { id = invoice.InvoiceId }).ToList();
                return invoice;
            }
        }

        public IList<LeaseContract> Contracts()
        {
            using (var c = _db.Open())
                return c.Query<LeaseContract>(@"SELECT ContractId, ContractNo, CustomerId, AssetCategory, AssetDescription, StartDate, EndDate,
                                                       MonthlyRent, AssetValue, ResidualValue, Status FROM stm.LeaseContract").ToList();
        }

        public IList<LeaseContract> ContractsForCustomer(int customerId)
        {
            using (var c = _db.Open())
                return c.Query<LeaseContract>(@"SELECT ContractId, ContractNo, CustomerId, AssetCategory, AssetDescription, StartDate, EndDate,
                                                       MonthlyRent, AssetValue, ResidualValue, Status FROM stm.LeaseContract
                                                 WHERE CustomerId = @customerId ORDER BY StartDate", new { customerId }).ToList();
        }

        public ISet<DateTime> Holidays()
        {
            using (var c = _db.Open())
                return new HashSet<DateTime>(c.Query<DateTime>("SELECT HolidayDate FROM stm.Holiday"));
        }

        public void LogRequest(DocumentRequest request)
        {
            using (var c = _db.Open())
                c.Execute(@"INSERT stm.DocumentRequest (Channel, RequestedBy, ReportCode, Parameters, OutputFormat, Bytes, DurationMs, Succeeded, Error)
                            VALUES (@Channel, @RequestedBy, @ReportCode, @Parameters, @OutputFormat, @Bytes, @DurationMs, @Succeeded, @Error)", request);
        }
    }
}
