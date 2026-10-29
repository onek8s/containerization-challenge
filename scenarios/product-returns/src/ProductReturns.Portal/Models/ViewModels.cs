using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ProductReturns.Core;
using ProductReturns.Core.Api;

namespace ProductReturns.Portal.Models
{
    public class StartReturnModel
    {
        [Required(ErrorMessage = "Enter your order number.")]
        [Display(Name = "Order number")]
        [RegularExpression(@"^\s*[Ss][Oo]-?\d{5,10}\s*$", ErrorMessage = "Order numbers look like SO-1048213.")]
        public string OrderNumber { get; set; }

        [Required(ErrorMessage = "Enter the e-mail address used for the order.")]
        [EmailAddress]
        [Display(Name = "E-mail address")]
        public string Email { get; set; }
    }

    public class ReturnDraft
    {
        public string OrderNumber { get; set; }
        public string Email { get; set; }
        public string CustomerName { get; set; }
        public List<CreateReturnLine> Lines { get; set; } = new List<CreateReturnLine>();
    }

    public class LineSelection
    {
        public int LineNumber { get; set; }
        public int Quantity { get; set; }
        public string ReasonCode { get; set; }

        [StringLength(1000)]
        public string Comment { get; set; }
    }

    public class SelectLinesModel
    {
        public OrderLookupResponse Order { get; set; }
        public List<LineSelection> Selections { get; set; } = new List<LineSelection>();
    }

    public class ReviewModel
    {
        public ReturnDraft Draft { get; set; }
        public OrderLookupResponse Order { get; set; }
        public decimal ItemsValue { get; set; }
        public decimal LabelFee { get; set; }
    }

    public class StatusLookupModel
    {
        [Required(ErrorMessage = "Enter your return number.")]
        [Display(Name = "Return number")]
        public string RmaNumber { get; set; }

        [Required(ErrorMessage = "Enter the e-mail address used for the order.")]
        [EmailAddress]
        [Display(Name = "E-mail address")]
        public string Email { get; set; }
    }

    public class CustomerReturnModel
    {
        public ReturnDetail Return { get; set; }
        public string Email { get; set; }
    }

    public class StaffLoginModel
    {
        [Required]
        [Display(Name = "User name")]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class StaffQueueFilter
    {
        public ReturnStatus? Status { get; set; } = ReturnStatus.Submitted;
        public string Query { get; set; }
        public int Page { get; set; } = 1;
    }

    public class StaffQueueModel
    {
        public StaffQueueFilter Filter { get; set; }
        public ReturnSearchResponse Result { get; set; }
        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
        public string Error { get; set; }
    }

    public class StaffDetailModel
    {
        public ReturnDetail Return { get; set; }
        public string Error { get; set; }
        public bool IsSupervisor { get; set; }
    }

    public class ErrorViewModel
    {
        public string RequestId { get; set; }
    }
}
