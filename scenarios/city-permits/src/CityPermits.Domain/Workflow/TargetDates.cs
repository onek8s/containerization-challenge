using CityPermits.Domain.Model;

namespace CityPermits.Domain.Workflow;

/// <summary>Statutory and service-level decision periods in working days.</summary>
public static class TargetDates
{
    public static int WorkingDaysFor(PermitCase c) => c.Kind switch
    {
        PermitKind.BuildingPermit when c.Building?.WorkType is WorkType.NewBuilding or WorkType.ChangeOfUse => 40,
        PermitKind.BuildingPermit => 25,
        PermitKind.BusinessLicence when c.Business?.Category is BusinessCategory.AlcoholOnSale or BusinessCategory.Entertainment => 28,
        PermitKind.BusinessLicence => 20,
        PermitKind.ParkingPermit => 5,
        _ => 20
    };

    public static DateOnly AddWorkingDays(DateOnly start, int days, ISet<DateOnly> holidays)
    {
        var d = start;
        var added = 0;
        while (added < days)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (holidays.Contains(d)) continue;
            added++;
        }
        return d;
    }

    public static int WorkingDaysBetween(DateOnly from, DateOnly to, ISet<DateOnly> holidays)
    {
        if (to <= from) return 0;
        var n = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(d)) n++;
        return n;
    }
}
