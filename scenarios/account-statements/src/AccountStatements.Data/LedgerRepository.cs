using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Model;

namespace Woodgrove.Statements.Data
{
    public class LedgerRepository
    {
        const string Columns = "EntryId, CustomerId, EntryType, DocumentNo, ContractNo, PostingDate, DueDate, Description, Amount, OpenAmount, ClosedDate, PaymentReference";
        readonly Db _db;

        public LedgerRepository(Db db) { _db = db; }

        public IList<LedgerEntry> ForCustomer(int customerId, DateTime upTo)
        {
            using (var c = _db.Open())
                return c.Query<LedgerEntry>("SELECT " + Columns + " FROM stm.LedgerEntry WHERE CustomerId = @customerId AND PostingDate <= @upTo",
                                            new { customerId, upTo = upTo.Date }).ToList();
        }

        public IList<LedgerEntry> All(DateTime upTo)
        {
            using (var c = _db.Open())
                return c.Query<LedgerEntry>("SELECT " + Columns + " FROM stm.LedgerEntry WHERE PostingDate <= @upTo", new { upTo = upTo.Date },
                                            commandTimeout: 300).ToList();
        }

        public IList<TransactionRow> Transactions(DateTime from, DateTime to, string creditController)
        {
            using (var c = _db.Open())
                return c.Query<TransactionRow>(@"
SELECT e.PostingDate, c.CustomerNo, c.Name AS CustomerName, c.CreditController, e.EntryType, e.DocumentNo, e.ContractNo,
       e.DueDate, e.Description, e.Amount, e.OpenAmount, e.PaymentReference
  FROM stm.LedgerEntry e
  JOIN stm.Customer c ON c.CustomerId = e.CustomerId
 WHERE e.PostingDate BETWEEN @from AND @to
   AND (@creditController IS NULL OR c.CreditController = @creditController)
 ORDER BY e.PostingDate, c.CustomerNo, e.DocumentNo",
                    new { from = from.Date, to = to.Date, creditController = string.IsNullOrWhiteSpace(creditController) ? null : creditController },
                    commandTimeout: 300).ToList();
        }

        public LedgerLoad LastLoad()
        {
            using (var c = _db.Open())
                return c.QueryFirstOrDefault<LedgerLoad>("SELECT TOP (1) LoadedAt, LedgerDate, Entries, Source FROM stm.LedgerLoad ORDER BY LoadId DESC");
        }
    }
}
