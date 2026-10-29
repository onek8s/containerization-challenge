using System;
using System.Collections.Generic;
using Woodgrove.Statements.Core.Ledger;

namespace Woodgrove.Statements.Tests
{
    static class TestData
    {
        public static Customer Customer(int id = 1, decimal creditLimit = 0m, string language = "en-GB")
        {
            return new Customer
            {
                CustomerId = id, CustomerNo = "C" + (10000 + id), Name = "Customer " + id, AddressLine1 = "Street 1", PostalCode = "00100", City = "Helsinki",
                CountryCode = "FI", Language = language, CurrencyCode = "EUR", Delivery = StatementDelivery.Email, Email = "ap@example.fi",
                CreditController = "Anna Virtanen", CreditLimit = creditLimit
            };
        }

        static long _id;

        public static LedgerEntry Invoice(int customerId, string no, DateTime date, decimal amount, DateTime? paid = null, decimal? open = null)
        {
            return new LedgerEntry
            {
                EntryId = ++_id, CustomerId = customerId, EntryType = EntryTypes.Invoice, DocumentNo = no, PostingDate = date, DueDate = date.AddDays(13),
                Amount = amount, OpenAmount = open ?? (paid.HasValue ? 0m : amount), ClosedDate = paid, Description = "Rent"
            };
        }

        public static LedgerEntry Payment(int customerId, string no, DateTime date, decimal amount, bool allocated = true)
        {
            return new LedgerEntry
            {
                EntryId = ++_id, CustomerId = customerId, EntryType = EntryTypes.Payment, DocumentNo = no, PostingDate = date,
                Amount = -amount, OpenAmount = allocated ? 0m : -amount, ClosedDate = allocated ? date : (DateTime?)null
            };
        }

        public static DateTime D(int year, int month, int day)
        {
            return new DateTime(year, month, day);
        }

        public static List<LedgerEntry> List(params LedgerEntry[] entries)
        {
            return new List<LedgerEntry>(entries);
        }
    }
}
