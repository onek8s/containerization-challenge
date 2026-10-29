using System.Collections.Generic;

namespace ProductReturns.Core
{
    public class ReturnPolicyOptions
    {
        /// <summary>Days after delivery a customer can start a return.</summary>
        public int DefaultWindowDays { get; set; } = 30;

        /// <summary>Return window per product category, overrides <see cref="DefaultWindowDays"/>.</summary>
        public Dictionary<string, int> CategoryWindowDays { get; set; } = new Dictionary<string, int>
        {
            ["Electronics"] = 14,
            ["Furniture"] = 60
        };

        /// <summary>Categories that can never be returned.</summary>
        public List<string> NonReturnableCategories { get; set; } = new List<string> { "Food", "GiftCards", "Hygiene" };

        /// <summary>Orders delivered between these month/day pairs (inclusive) can be returned until <see cref="HolidayReturnUntil"/>.</summary>
        public string HolidayFrom { get; set; } = "11-15";
        public string HolidayTo { get; set; } = "12-24";
        public string HolidayReturnUntil { get; set; } = "01-31";

        /// <summary>Return label price charged when the customer is returning by choice.</summary>
        public decimal LabelFee { get; set; } = 5.95m;

        /// <summary>Restocking fee for opened items returned by choice, per category, as a fraction of the line amount.</summary>
        public Dictionary<string, decimal> OpenedRestockingFee { get; set; } = new Dictionary<string, decimal>
        {
            ["Electronics"] = 0.15m,
            ["Appliances"] = 0.10m
        };

        /// <summary>Share of the line amount refunded for used items.</summary>
        public decimal UsedRefundShare { get; set; } = 0.50m;

        /// <summary>Approved returns that are not received within this many days are reminded once.</summary>
        public int ReminderAfterDays { get; set; } = 10;

        /// <summary>Approved returns that are not received within this many days are closed.</summary>
        public int ExpireAfterDays { get; set; } = 45;
    }
}
