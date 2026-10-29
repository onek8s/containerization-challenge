using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Domain.Orders
{
    /// <summary>
    /// Milestones of the OrderLifecycle BAM activity. The names are the BAM activity item names (column names in
    /// bam_OrderLifecycle_Completed / _Active).
    /// </summary>
    public enum OrderMilestone
    {
        Received = 1,
        Validated = 2,
        CreditHold = 3,
        Released = 4,
        SentToErp = 5,
        ConfirmedByErp = 6,
        Despatched = 7,
        Invoiced = 8,
        Rejected = 9
    }

    public class OrderLifecycle
    {
        private readonly Dictionary<OrderMilestone, DateTime> _milestones = new Dictionary<OrderMilestone, DateTime>();

        public OrderLifecycle(string activityId, string partnerId, string partnerOrderNumber)
        {
            ActivityId = activityId;
            PartnerId = partnerId;
            PartnerOrderNumber = partnerOrderNumber;
        }

        public string ActivityId { get; private set; }
        public string PartnerId { get; private set; }
        public string PartnerOrderNumber { get; private set; }
        public string ErpOrderNumber { get; set; }
        public string InterchangeControlNumber { get; set; }
        public decimal OrderValue { get; set; }
        public int LineCount { get; set; }
        public string RejectReason { get; set; }
        public string HoldReason { get; set; }

        public IDictionary<OrderMilestone, DateTime> Milestones
        {
            get { return _milestones; }
        }

        public bool IsCompleted
        {
            get { return _milestones.ContainsKey(OrderMilestone.Invoiced) || _milestones.ContainsKey(OrderMilestone.Rejected); }
        }

        public OrderMilestone? Current
        {
            get
            {
                if (_milestones.Count == 0)
                {
                    return null;
                }

                return _milestones.OrderBy(m => m.Value).ThenBy(m => (int)m.Key).Last().Key;
            }
        }

        public void Record(OrderMilestone milestone, DateTime atUtc)
        {
            if (_milestones.ContainsKey(OrderMilestone.Rejected) && milestone != OrderMilestone.Rejected)
            {
                throw new InvalidOperationException("Order " + PartnerOrderNumber + " was rejected; milestone " + milestone + " is not allowed.");
            }

            if (milestone == OrderMilestone.SentToErp && _milestones.ContainsKey(OrderMilestone.CreditHold) && !_milestones.ContainsKey(OrderMilestone.Released))
            {
                throw new InvalidOperationException("Order " + PartnerOrderNumber + " is on credit hold and has not been released.");
            }

            _milestones[milestone] = atUtc;
        }

        public DateTime? At(OrderMilestone milestone)
        {
            DateTime value;
            return _milestones.TryGetValue(milestone, out value) ? value : (DateTime?)null;
        }

        public TimeSpan? Duration(OrderMilestone from, OrderMilestone to)
        {
            var a = At(from);
            var b = At(to);
            return a.HasValue && b.HasValue ? b.Value - a.Value : (TimeSpan?)null;
        }
    }
}
