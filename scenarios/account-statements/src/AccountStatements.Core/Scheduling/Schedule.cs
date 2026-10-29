using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Woodgrove.Statements.Core.Scheduling
{
    public enum ScheduleKind
    {
        Daily,
        Workdays,
        Weekly,
        MonthlyDay,
        MonthlyLast,
        MonthlyWorkday
    }

    /// <summary>
    /// Subscription schedule, stored as text in stm.Subscription.Schedule:
    ///   DAILY 05:30 | WORKDAYS 06:00 | WEEKLY MON,THU 07:30 | MONTHLY 1 06:00 | MONTHLY LAST 18:00 | MONTHLY WD3 06:00
    /// Times are server local time.
    /// </summary>
    public sealed class Schedule
    {
        static readonly string[] DayNames = { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };

        public ScheduleKind Kind { get; private set; }
        public TimeSpan Time { get; private set; }
        public int Day { get; private set; }
        public IReadOnlyList<DayOfWeek> Days { get; private set; }

        Schedule() { Days = new DayOfWeek[0]; }

        public static Schedule Parse(string text)
        {
            Schedule s;
            string error;
            if (!TryParse(text, out s, out error)) throw new FormatException(error);
            return s;
        }

        public static bool TryParse(string text, out Schedule schedule, out string error)
        {
            schedule = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text)) { error = "Schedule is empty."; return false; }
            var parts = text.Trim().ToUpperInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var s = new Schedule();
            TimeSpan time;
            if (!TryParseTime(parts[parts.Length - 1], out time)) { error = "Schedule must end with a time (HH:mm)."; return false; }
            s.Time = time;

            switch (parts[0])
            {
                case "DAILY":
                case "WORKDAYS":
                    if (parts.Length != 2) { error = parts[0] + " takes only a time."; return false; }
                    s.Kind = parts[0] == "DAILY" ? ScheduleKind.Daily : ScheduleKind.Workdays;
                    break;
                case "WEEKLY":
                    if (parts.Length != 3) { error = "WEEKLY needs days and a time, e.g. WEEKLY MON,THU 07:30."; return false; }
                    var days = new List<DayOfWeek>();
                    foreach (var d in parts[1].Split(','))
                    {
                        var i = Array.IndexOf(DayNames, d);
                        if (i < 0) { error = "Unknown day '" + d + "'."; return false; }
                        if (!days.Contains((DayOfWeek)i)) days.Add((DayOfWeek)i);
                    }
                    s.Kind = ScheduleKind.Weekly;
                    s.Days = days.OrderBy(d => d).ToArray();
                    break;
                case "MONTHLY":
                    if (parts.Length != 3) { error = "MONTHLY needs a day and a time, e.g. MONTHLY 1 06:00."; return false; }
                    int day;
                    if (parts[1] == "LAST")
                        s.Kind = ScheduleKind.MonthlyLast;
                    else if (parts[1].StartsWith("WD", StringComparison.Ordinal) && int.TryParse(parts[1].Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out day) && day >= 1 && day <= 20)
                    {
                        s.Kind = ScheduleKind.MonthlyWorkday;
                        s.Day = day;
                    }
                    else if (int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out day) && day >= 1 && day <= 31)
                    {
                        s.Kind = ScheduleKind.MonthlyDay;
                        s.Day = day;
                    }
                    else { error = "Unknown monthly day '" + parts[1] + "'."; return false; }
                    break;
                default:
                    error = "Unknown schedule type '" + parts[0] + "'.";
                    return false;
            }
            schedule = s;
            return true;
        }

        static bool TryParseTime(string text, out TimeSpan time)
        {
            DateTime dt;
            time = TimeSpan.Zero;
            if (!DateTime.TryParseExact(text, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return false;
            time = dt.TimeOfDay;
            return true;
        }

        /// <summary>First occurrence strictly after <paramref name="after"/>.</summary>
        public DateTime NextRun(DateTime after, ICollection<DateTime> holidays = null)
        {
            holidays = holidays ?? new HashSet<DateTime>();
            var date = after.Date;
            for (var i = 0; i < 800; i++, date = date.AddDays(1))
            {
                var candidate = date + Time;
                if (candidate <= after) continue;
                if (Matches(date, holidays)) return candidate;
            }
            throw new InvalidOperationException("No occurrence found for schedule " + ToString() + ".");
        }

        bool Matches(DateTime date, ICollection<DateTime> holidays)
        {
            switch (Kind)
            {
                case ScheduleKind.Daily:
                    return true;
                case ScheduleKind.Workdays:
                    return IsWorkday(date, holidays);
                case ScheduleKind.Weekly:
                    return Days.Contains(date.DayOfWeek);
                case ScheduleKind.MonthlyDay:
                    return date.Day == Math.Min(Day, DateTime.DaysInMonth(date.Year, date.Month));
                case ScheduleKind.MonthlyLast:
                    return date.Day == DateTime.DaysInMonth(date.Year, date.Month);
                case ScheduleKind.MonthlyWorkday:
                    return date == NthWorkday(date.Year, date.Month, Day, holidays);
                default:
                    return false;
            }
        }

        public static bool IsWorkday(DateTime date, ICollection<DateTime> holidays)
        {
            return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday && !holidays.Contains(date.Date);
        }

        public static DateTime NthWorkday(int year, int month, int n, ICollection<DateTime> holidays)
        {
            var d = new DateTime(year, month, 1);
            var count = 0;
            while (d.Month == month)
            {
                if (IsWorkday(d, holidays) && ++count == n) return d;
                d = d.AddDays(1);
            }
            return DateTime.MinValue;
        }

        public override string ToString()
        {
            var t = Time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            switch (Kind)
            {
                case ScheduleKind.Daily: return "DAILY " + t;
                case ScheduleKind.Workdays: return "WORKDAYS " + t;
                case ScheduleKind.Weekly: return "WEEKLY " + string.Join(",", Days.Select(d => DayNames[(int)d])) + " " + t;
                case ScheduleKind.MonthlyDay: return "MONTHLY " + Day.ToString(CultureInfo.InvariantCulture) + " " + t;
                case ScheduleKind.MonthlyLast: return "MONTHLY LAST " + t;
                default: return "MONTHLY WD" + Day.ToString(CultureInfo.InvariantCulture) + " " + t;
            }
        }

        public string Describe()
        {
            var t = Time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            switch (Kind)
            {
                case ScheduleKind.Daily: return "Every day at " + t;
                case ScheduleKind.Workdays: return "Monday to Friday at " + t;
                case ScheduleKind.Weekly: return "Every " + string.Join(", ", Days.Select(d => CultureInfo.InvariantCulture.DateTimeFormat.GetDayName(d))) + " at " + t;
                case ScheduleKind.MonthlyDay: return "Monthly on day " + Day + " at " + t;
                case ScheduleKind.MonthlyLast: return "Last day of the month at " + t;
                default: return "Working day " + Day + " of the month at " + t;
            }
        }
    }
}
