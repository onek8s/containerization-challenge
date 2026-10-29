using System;
using System.Collections.Generic;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;

namespace Tailspin.DealerOrders.SalesDesk.Models
{
    public class DeskDashboard
    {
        public DashboardFigures Figures { get; set; }
        public IList<ApprovalRow> Approvals { get; set; }
        public IList<OrderListItem> OrdersInError { get; set; }
        public QueueCounts Queue { get; set; }
        public int EdiProblems { get; set; }
        public DateTime? LastPriceSync { get; set; }
        public DateTime? LastStockSync { get; set; }
    }

    public class OrderDetails
    {
        public SalesOrder Order { get; set; }
        public Dealer Dealer { get; set; }
        public IList<AxCommand> Commands { get; set; }
        public IList<ApprovalRequest> Approvals { get; set; }
        public IList<EdiMessage> EdiMessages { get; set; }
    }

    public class DealerDetails
    {
        public Dealer Dealer { get; set; }
        public decimal OpenOrderValue { get; set; }
        public IList<OrderListItem> Orders { get; set; }
        public EdiPartner EdiPartner { get; set; }
    }
}
