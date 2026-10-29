using System;

namespace Wingtip.FieldSales.Core
{
    /// <summary>Weekdays on which the delivery route visits the customer. Stored as a bit mask in the database.</summary>
    [Flags]
    public enum DeliveryDays
    {
        None = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 4,
        Thursday = 8,
        Friday = 16
    }

    public static class DeliveryDaysExtensions
    {
        public static bool Includes(this DeliveryDays days, DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday: return (days & DeliveryDays.Monday) != 0;
                case DayOfWeek.Tuesday: return (days & DeliveryDays.Tuesday) != 0;
                case DayOfWeek.Wednesday: return (days & DeliveryDays.Wednesday) != 0;
                case DayOfWeek.Thursday: return (days & DeliveryDays.Thursday) != 0;
                case DayOfWeek.Friday: return (days & DeliveryDays.Friday) != 0;
                default: return false;
            }
        }

        public static string ToShortText(this DeliveryDays days)
        {
            if (days == DeliveryDays.None) return "-";
            var text = "";
            if ((days & DeliveryDays.Monday) != 0) text += "Mo ";
            if ((days & DeliveryDays.Tuesday) != 0) text += "Tu ";
            if ((days & DeliveryDays.Wednesday) != 0) text += "We ";
            if ((days & DeliveryDays.Thursday) != 0) text += "Th ";
            if ((days & DeliveryDays.Friday) != 0) text += "Fr ";
            return text.TrimEnd();
        }
    }
}
