using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Data.Repositories
{
    public class CustomerMergeResult
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Unchanged { get; set; }
    }

    public class CustomerRepository
    {
        public IList<Customer> Search(string text, int max)
        {
            text = (text ?? string.Empty).Trim();
            using (var db = new CareDbContext())
            {
                IQueryable<Customer> q = db.Customers.AsNoTracking();
                if (text.Length > 0)
                {
                    q = q.Where(c => c.AccountNumber.StartsWith(text) || c.Name.Contains(text) || c.Email.StartsWith(text) || c.Phone.StartsWith(text));
                }

                return q.OrderBy(c => c.Name).Take(max).ToList();
            }
        }

        public Customer Get(int id)
        {
            using (var db = new CareDbContext())
            {
                return db.Customers.AsNoTracking().FirstOrDefault(c => c.Id == id);
            }
        }

        public Customer GetByAccount(string accountNumber)
        {
            using (var db = new CareDbContext())
            {
                return db.Customers.AsNoTracking().FirstOrDefault(c => c.AccountNumber == accountNumber);
            }
        }

        public void Update(Customer customer)
        {
            using (var db = new CareDbContext())
            {
                var existing = db.Customers.Find(customer.Id);
                if (existing == null) throw new InvalidOperationException("Customer " + customer.Id + " not found.");

                existing.Name = customer.Name;
                existing.Email = customer.Email;
                existing.Phone = customer.Phone;
                existing.Street = customer.Street;
                existing.PostalCode = customer.PostalCode;
                existing.City = customer.City;
                existing.IsPriorityCustomer = customer.IsPriorityCustomer;
                existing.ModifiedUtc = DateTime.UtcNow;
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Loads the rows into the staging table with SqlBulkCopy and merges them into care.Customer in one statement.
        /// </summary>
        public CustomerMergeResult MergeImport(IEnumerable<CustomerImportRow> rows, string sourceFile)
        {
            var batchId = Guid.NewGuid();
            var table = new DataTable();
            table.Columns.Add("BatchId", typeof(Guid));
            table.Columns.Add("AccountNumber", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Email", typeof(string));
            table.Columns.Add("Phone", typeof(string));
            table.Columns.Add("Street", typeof(string));
            table.Columns.Add("PostalCode", typeof(string));
            table.Columns.Add("City", typeof(string));
            table.Columns.Add("Segment", typeof(string));
            table.Columns.Add("IsPriorityCustomer", typeof(bool));

            foreach (var r in rows)
            {
                table.Rows.Add(batchId, r.AccountNumber, r.Name, (object)r.Email ?? DBNull.Value, (object)r.Phone ?? DBNull.Value,
                    (object)r.Street ?? DBNull.Value, r.PostalCode, (object)r.City ?? DBNull.Value, r.Segment, r.IsPriorityCustomer);
            }

            using (var cn = Db.Open())
            {
                using (var bulk = new SqlBulkCopy(cn) { DestinationTableName = "care.CustomerImportStaging", BatchSize = 5000, BulkCopyTimeout = 600 })
                {
                    foreach (DataColumn c in table.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
                    bulk.WriteToServer(table);
                }

                using (var cmd = Db.Proc(cn, "care.usp_Customer_MergeStaging", Db.P("@BatchId", batchId), Db.P("@SourceFile", sourceFile)))
                {
                    cmd.CommandTimeout = 600;
                    using (var r = cmd.ExecuteReader())
                    {
                        r.Read();
                        return new CustomerMergeResult { Inserted = r.GetInt("Inserted"), Updated = r.GetInt("Updated"), Unchanged = r.GetInt("Unchanged") };
                    }
                }
            }
        }
    }
}
