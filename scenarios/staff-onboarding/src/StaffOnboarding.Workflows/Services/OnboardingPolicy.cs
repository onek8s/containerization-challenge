using System;
using StaffOnboarding.Core.Access;
using StaffOnboarding.Core.Scheduling;

namespace StaffOnboarding.Workflows.Services
{
    /// <summary>Access map, calendar and approver settings shared by all workflow instances on this host.</summary>
    public class OnboardingPolicy
    {
        public OnboardingPolicy(WorkflowSettings settings)
        {
            Settings = settings;
            Planner = new AccessPlanner(AccessMap.Load(settings.AccessMapPath));
            Schedule = new OnboardingSchedule(
                BusinessCalendar.Load(settings.HolidaysPath),
                settings.Schedule,
                TimeZoneInfo.FindSystemTimeZoneById(settings.OfficeTimeZone));
        }

        public WorkflowSettings Settings { get; private set; }
        public AccessPlanner Planner { get; private set; }
        public OnboardingSchedule Schedule { get; private set; }
    }
}
