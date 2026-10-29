using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CreditDesk.Contracts;
using CreditDesk.Core;

namespace CreditDesk.Web.Models
{
    public class HomeModel
    {
        public UserInfo User { get; set; }
        public IList<CreditRequest> MyRequests { get; set; }
        public int PendingApprovals { get; set; }
    }

    public class CustomerSearchModel
    {
        public string Query { get; set; }
        public IList<CustomerAccount> Results { get; set; }
    }

    public class CustomerDetailsModel
    {
        public CustomerAccount Customer { get; set; }
        public IList<CreditRequest> History { get; set; }
        public NewRequestModel NewRequest { get; set; }
    }

    public class NewRequestModel
    {
        [Required]
        public string AccountNumber { get; set; }

        [Required, Range(0, 100000000)]
        [Display(Name = "New credit limit")]
        public decimal? RequestedLimit { get; set; }

        [Required, StringLength(1000, MinimumLength = 10)]
        public string Reason { get; set; }
    }

    public class DecisionModel
    {
        public int RequestId { get; set; }
        public bool Approve { get; set; }

        [StringLength(1000)]
        public string Comment { get; set; }
    }

    public class ServiceErrorModel
    {
        public string Message { get; set; }
        public string[] Errors { get; set; }
    }
}
