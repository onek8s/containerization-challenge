using System;
using System.Runtime.Serialization;

namespace CreditDesk.Core
{
    [DataContract(Namespace = Namespaces.Data)]
    public class CustomerAccount
    {
        [DataMember] public string AccountNumber { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string ContactName { get; set; }
        [DataMember] public string AddressLine1 { get; set; }
        [DataMember] public string AddressLine2 { get; set; }
        [DataMember] public string PostalCode { get; set; }
        [DataMember] public string City { get; set; }
        [DataMember] public string CountryCode { get; set; }
        [DataMember] public string SalesRep { get; set; }
        [DataMember] public decimal CreditLimit { get; set; }
        [DataMember] public decimal OpenBalance { get; set; }
        [DataMember] public decimal OverdueBalance { get; set; }
        [DataMember] public int OldestOverdueDays { get; set; }
        [DataMember] public DateTime? CustomerSince { get; set; }
        [DataMember] public bool OnStop { get; set; }

        public decimal AvailableCredit => CreditLimit - OpenBalance;
    }
}
