using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StaffOnboarding.Core.Access;
using StaffOnboarding.Core.Scheduling;

namespace StaffOnboarding.Portal.Services;

/// <summary>Departments and roles from the IT access map, and the company calendar, read from the onboarding share.</summary>
public sealed class ReferenceData
{
    private readonly IMemoryCache _cache;
    private readonly PortalOptions _options;

    public ReferenceData(IMemoryCache cache, IOptions<PortalOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public AccessMap AccessMap => _cache.GetOrCreate("access-map", e =>
    {
        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
        return AccessMap.Load(_options.AccessMapPath);
    })!;

    public BusinessCalendar Calendar => _cache.GetOrCreate("holidays", e =>
    {
        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
        return BusinessCalendar.Load(_options.HolidaysPath);
    })!;

    /// <summary>Earliest start date with the workflow host's default lead time.</summary>
    public DateTime EarliestStartDate(DateTime today) =>
        new OnboardingSchedule(Calendar, new ScheduleOptions(), TimeZoneInfo.Local).EarliestStartDate(today);

    /// <summary>Groups that HR may request in addition to the role defaults.</summary>
    public IEnumerable<string> RequestableGroups => AccessMap.Requestable.Select(g => g.Name);

    public IEnumerable<string> GroupAssignees(PortalOptions options) => new[]
    {
        "group:" + options.ItGroup, "group:" + options.ItGroup + "-Leads", "group:" + options.HrGroup
    };
}
