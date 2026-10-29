using System;

namespace CreditDesk.Core
{
    /// <summary>Body of the message sent to the letter queue after a decision.</summary>
    [Serializable]
    public class LetterMessage
    {
        public int RequestId { get; set; }
        public string AccountNumber { get; set; }
        public string CustomerName { get; set; }
        public string ContactName { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string CountryCode { get; set; }
        public decimal PreviousLimit { get; set; }
        public decimal NewLimit { get; set; }
        public bool Approved { get; set; }
        public string DecisionComment { get; set; }
        public DateTime DecidedAt { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string SignedBy { get; set; }
        public string SignedByTitle { get; set; }
        public string Language { get; set; } = "en";
    }
}
