using System;
using System.Collections.Generic;

namespace Coho.Billing.Core.Output
{
    public class CreditorSettings
    {
        public string Name { get; set; }
        public string Iban { get; set; }
        public string Bic { get; set; }

        /// <summary>SEPA creditor identifier.</summary>
        public string CreditorId { get; set; }
    }

    public class DirectDebitItem
    {
        public string EndToEndId { get; set; }
        public decimal Amount { get; set; }
        public string MandateId { get; set; }
        public DateTime MandateSignedOn { get; set; }
        public bool FirstCollection { get; set; }
        public string DebtorName { get; set; }
        public string DebtorIban { get; set; }
        public string CreditorReference { get; set; }
    }

    public class DirectDebitBatch
    {
        public DirectDebitBatch()
        {
            Items = new List<DirectDebitItem>();
        }

        public string MessageId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime CollectionDate { get; set; }
        public List<DirectDebitItem> Items { get; private set; }
    }
}
