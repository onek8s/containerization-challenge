using System.Collections.Generic;
using System.Linq;
using Dapper;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Model;

namespace Woodgrove.Statements.Data
{
    public class CustomerRepository
    {
        const string Columns = @"CustomerId, CustomerNo, Name, AddressLine1, AddressLine2, PostalCode, City, CountryCode, Email, Language,
                                 CurrencyCode, CAST(Delivery AS int) AS Delivery, DropFolder, CreditController, CreditLimit, OnHold";
        readonly Db _db;

        public CustomerRepository(Db db) { _db = db; }

        public IList<Customer> All()
        {
            using (var c = _db.Open())
                return c.Query<Customer>("SELECT " + Columns + " FROM stm.Customer ORDER BY CustomerNo").ToList();
        }

        public Customer Get(string customerNo)
        {
            using (var c = _db.Open())
                return c.QuerySingleOrDefault<Customer>("SELECT " + Columns + " FROM stm.Customer WHERE CustomerNo = @customerNo", new { customerNo });
        }

        public Customer GetById(int customerId)
        {
            using (var c = _db.Open())
                return c.QuerySingleOrDefault<Customer>("SELECT " + Columns + " FROM stm.Customer WHERE CustomerId = @customerId", new { customerId });
        }

        public IList<CustomerBalance> Search(string term, string creditController, int max = 200)
        {
            using (var c = _db.Open())
                return c.Query<CustomerBalance>(@"
SELECT TOP (@max) CustomerId, CustomerNo, Name, CreditController, CreditLimit, OnHold, Delivery, OpenBalance, Overdue, LastPaymentDate
  FROM stm.CustomerBalance
 WHERE (@term IS NULL OR CustomerNo LIKE @like OR Name LIKE @like)
   AND (@creditController IS NULL OR CreditController = @creditController)
 ORDER BY Overdue DESC, CustomerNo",
                    new { max, term = string.IsNullOrWhiteSpace(term) ? null : term, like = "%" + (term ?? "").Trim() + "%", creditController = string.IsNullOrWhiteSpace(creditController) ? null : creditController }).ToList();
        }

        public IList<string> CreditControllers()
        {
            using (var c = _db.Open())
                return c.Query<string>("SELECT DISTINCT CreditController FROM stm.Customer WHERE CreditController IS NOT NULL ORDER BY CreditController").ToList();
        }

        public void UpdateDelivery(int customerId, StatementDelivery delivery, string email, string dropFolder)
        {
            using (var c = _db.Open())
                c.Execute(@"UPDATE stm.Customer SET Delivery = @delivery, Email = @email, DropFolder = @dropFolder, ModifiedAt = SYSDATETIME()
                             WHERE CustomerId = @customerId",
                          new { customerId, delivery = (byte)delivery, email, dropFolder });
        }
    }
}
