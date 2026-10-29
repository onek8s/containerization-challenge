using FieldService.Data.Entities;

namespace FieldService.Data.Services;

public static class SlaCalculator
{
    /// <summary>
    /// Due time is the customer's contracted response window, shortened for urgent jobs.
    /// Weekends are not counted for low and normal priority work.
    /// </summary>
    public static DateTime DueDate(DateTime createdUtc, int customerSlaHours, WorkOrderPriority priority)
    {
        switch (priority)
        {
            case WorkOrderPriority.Emergency:
                return createdUtc.AddHours(4);
            case WorkOrderPriority.High:
                return createdUtc.AddHours(Math.Min(customerSlaHours, 24));
        }

        var hours = priority == WorkOrderPriority.Low ? customerSlaHours * 2 : customerSlaHours;
        var due = createdUtc;
        while (hours > 0)
        {
            due = due.AddHours(1);
            if (due.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                hours--;
            }
        }

        return due;
    }
}
