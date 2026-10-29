using System;
using System.IO;
using StaffOnboarding.Core.Scheduling;

namespace StaffOnboarding.Tests;

public class ScheduleTests
{
    private static readonly TimeZoneInfo Helsinki =
        TimeZoneInfo.CreateCustomTimeZone("Test Office", TimeSpan.FromHours(2), "Test Office", "Test Office");

    private static BusinessCalendar Calendar() =>
        BusinessCalendar.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "holidays.txt"));

    private static OnboardingSchedule Schedule() => new(Calendar(), new ScheduleOptions(), Helsinki);

    [Fact]
    public void Holiday_file_is_parsed()
    {
        var calendar = Calendar();
        Assert.False(calendar.IsBusinessDay(new DateTime(2026, 12, 24)));
        Assert.False(calendar.IsBusinessDay(new DateTime(2026, 11, 1)));
        Assert.True(calendar.IsBusinessDay(new DateTime(2026, 11, 2)));
    }

    [Fact]
    public void Invalid_holiday_line_reports_line_number()
    {
        var ex = Assert.Throws<FormatException>(() => BusinessCalendar.Parse(new StringReader("2026-01-01\n01.05.2026\n")));
        Assert.Contains("line 2", ex.Message);
    }

    [Fact]
    public void Joiner_account_is_created_three_business_days_before_start()
    {
        // Monday 2026-11-09 -> Wednesday 2026-11-04 06:00
        Assert.Equal(new DateTime(2026, 11, 4, 6, 0, 0), Schedule().JoinerProvisioningTime(new DateTime(2026, 11, 9)));
    }

    [Fact]
    public void Provisioning_skips_holidays_and_weekend_start_dates()
    {
        // Start on Sunday 2027-01-03 -> first day Monday 2027-01-04; three business days back skips 2026-12-31 and 2027-01-01
        var s = Schedule();
        Assert.Equal(new DateTime(2026, 12, 28, 6, 0, 0), s.JoinerProvisioningTime(new DateTime(2027, 1, 3)));
        Assert.Equal(new DateTime(2027, 1, 4, 6, 0, 0), s.JoinerEnableTime(new DateTime(2027, 1, 3)));
    }

    [Fact]
    public void Earliest_start_date_leaves_room_for_approvals()
    {
        Assert.Equal(new DateTime(2026, 11, 5), Schedule().EarliestStartDate(new DateTime(2026, 10, 29, 15, 30, 0)));
    }

    [Fact]
    public void Leaver_is_disabled_at_end_of_day_and_cleaned_up_after_thirty_days()
    {
        var s = Schedule();
        var last = new DateTime(2026, 11, 30);
        Assert.Equal(new DateTime(2026, 11, 30, 18, 0, 0), s.LeaverDisableTime(last));
        Assert.Equal(new DateTime(2026, 12, 30, 18, 0, 0), s.LeaverCleanupTime(last));
    }

    [Fact]
    public void Mover_old_access_is_removed_after_handover_business_days()
    {
        Assert.Equal(new DateTime(2026, 11, 9, 6, 0, 0), Schedule().MoverRemoveOldAccessTime(new DateTime(2026, 11, 2), 5));
    }

    [Fact]
    public void Account_expires_at_end_of_contract_day()
    {
        Assert.Equal(new DateTime(2027, 7, 1), Schedule().AccountExpiry(new DateTime(2027, 6, 30)));
        Assert.Null(Schedule().AccountExpiry(null));
    }

    [Fact]
    public void Delay_is_never_negative_and_uses_office_time_zone()
    {
        var s = Schedule();
        var due = new DateTime(2026, 11, 4, 6, 0, 0);
        Assert.Equal(new DateTime(2026, 11, 4, 4, 0, 0, DateTimeKind.Utc), s.ToUtc(due));
        Assert.Equal(TimeSpan.FromHours(1), s.DelayUntil(due, new DateTime(2026, 11, 4, 3, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(TimeSpan.Zero, s.DelayUntil(due, new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc)));
    }
}
