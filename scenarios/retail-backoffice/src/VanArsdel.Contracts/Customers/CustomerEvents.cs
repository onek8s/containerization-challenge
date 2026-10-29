using System;

namespace VanArsdel.Contracts.Customers
{
    public class LoyaltyTierChanged
    {
        public string CustomerId { get; set; }
        public string CardNo { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string FirstName { get; set; }
        public string PreviousTier { get; set; }
        public string NewTier { get; set; }
        public decimal RollingSpend { get; set; }
    }

    public class LoyaltyPointsEarned
    {
        public string CustomerId { get; set; }
        public string CardNo { get; set; }
        public int Points { get; set; }
        public string Source { get; set; }
        public string Reference { get; set; }
    }

    public class CustomerErased
    {
        public string CustomerId { get; set; }
        public string EmailHash { get; set; }
        public DateTime ErasedUtc { get; set; }
    }
}
