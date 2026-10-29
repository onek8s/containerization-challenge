using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.Configurator
{
    /// <summary>
    /// Earliest delivery for a built-to-order machine: model lead time plus the longest option lead time, counted in
    /// factory production weeks. The factory is closed for the summer shutdown (ISO weeks 28-31) and between Christmas and
    /// New Year (week 52 and 1), so those weeks are skipped.
    /// </summary>
    public class LeadTimeCalculator
    {
        private readonly HashSet<int> _closedWeeks;

        public LeadTimeCalculator(IEnumerable<int> closedWeeks = null)
        {
            _closedWeeks = new HashSet<int>(closedWeeks ?? new[] { 28, 29, 30, 31, 52, 1 });
        }

        public int LeadTimeWeeks(ProductModel model, MachineConfiguration configuration)
        {
            var extra = configuration.Options.Select(model.FindOption).Where(o => o != null).Select(o => o.ExtraLeadTimeWeeks).DefaultIfEmpty(0).Max();
            return model.LeadTimeWeeks + extra;
        }

        public DateTime EarliestDelivery(DateTime orderDate, int productionWeeks)
        {
            var date = orderDate.Date;
            var counted = 0;
            while (counted < productionWeeks)
            {
                date = date.AddDays(7);
                if (!_closedWeeks.Contains(IsoWeek(date))) counted++;
            }

            // Deliveries leave the factory on Thursdays.
            while (date.DayOfWeek != DayOfWeek.Thursday) date = date.AddDays(1);
            while (_closedWeeks.Contains(IsoWeek(date))) date = date.AddDays(7);
            return date;
        }

        public static int IsoWeek(DateTime date)
        {
            var day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday) date = date.AddDays(3);
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}
