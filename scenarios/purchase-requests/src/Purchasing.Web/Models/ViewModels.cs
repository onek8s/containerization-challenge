using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Contoso.Purchasing.Core;

namespace Contoso.Purchasing.Web.Models
{
    public class DashboardModel
    {
        public UserContext User { get; set; }
        public IList<RequestSummary> MyRequests { get; set; }
        public IList<RequestSummary> Approvals { get; set; }
        public IList<RequestSummary> ReadyToOrder { get; set; }
    }

    [Serializable]
    public class RequestForm
    {
        public RequestForm()
        {
            Lines = new List<LineForm>();
        }

        public int? Id { get; set; }
        public string Number { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required, Display(Name = "Cost center")]
        public string CostCenterCode { get; set; }

        [Required, Display(Name = "Supplier")]
        public int SupplierId { get; set; }

        [Required]
        public string Category { get; set; }

        [StringLength(2000)]
        public string Justification { get; set; }

        [Display(Name = "Needed by"), DataType(DataType.Date)]
        public DateTime? NeededBy { get; set; }

        public List<LineForm> Lines { get; set; }
    }

    [Serializable]
    public class LineForm
    {
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
    }

    public class RequestEditModel
    {
        public RequestForm Form { get; set; }
        public IList<CostCenter> CostCenters { get; set; }
        public IList<Supplier> Suppliers { get; set; }
        public IList<Category> Categories { get; set; }
    }

    public class OrderForm
    {
        public int RequestId { get; set; }

        [Required, Display(Name = "Delivery address"), StringLength(400)]
        public string DeliveryAddress { get; set; }

        public RequestView Request { get; set; }
    }
}
