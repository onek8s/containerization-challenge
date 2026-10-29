using System;
using System.Collections.Generic;

namespace VanArsdel.Customers.Api.Documents
{
    /// <summary>
    /// Customer profile with the loyalty account embedded. Stored as one document per customer
    /// (MongoDB collection "customers").
    /// </summary>
    public class CustomerDocument
    {
        public string Id { get; set; }
        public int Version { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Market { get; set; }
        public string PreferredStore { get; set; }
        public DateTime? BirthDate { get; set; }
        public Consents Consents { get; set; } = new Consents();
        public LoyaltyAccount Loyalty { get; set; }
        public List<Address> Addresses { get; set; } = new List<Address>();
        public string Source { get; set; }
        public bool Erased { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class Consents
    {
        public bool Email { get; set; }
        public bool Sms { get; set; }
        public bool Profiling { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }

    public class Address
    {
        public string Type { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
    }

    public class LoyaltyAccount
    {
        public string CardNo { get; set; }
        public string Tier { get; set; } = LoyaltyTiers.Bronze;
        public DateTime TierSince { get; set; }
        public int Points { get; set; }
        public decimal RollingSpend { get; set; }
        public DateTime JoinedUtc { get; set; }
        public List<PointsTransaction> Transactions { get; set; } = new List<PointsTransaction>();
    }

    public class PointsTransaction
    {
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public int Points { get; set; }
        public decimal AmountEur { get; set; }
        public string Source { get; set; }
        public string Reference { get; set; }
    }

    public static class LoyaltyTiers
    {
        public const string Bronze = "Bronze";
        public const string Silver = "Silver";
        public const string Gold = "Gold";
        public const string Platinum = "Platinum";
    }
}
