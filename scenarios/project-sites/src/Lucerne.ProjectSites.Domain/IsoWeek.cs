using System;
using System.Globalization;

namespace Lucerne.ProjectSites.Domain
{
    /// <summary>ISO 8601 week, used for timesheet periods (the ERP books hours per ISO week).</summary>
    public struct IsoWeek : IEquatable<IsoWeek>, IComparable<IsoWeek>
    {
        public IsoWeek(int year, int week)
        {
            if (week < 1 || week > WeeksInYear(year))
            {
                throw new ArgumentOutOfRangeException(nameof(week));
            }

            Year = year;
            Week = week;
        }

        public int Year { get; }
        public int Week { get; }

        public static IsoWeek FromDate(DateTime date)
        {
            var d = date.Date;
            var dow = ((int)d.DayOfWeek + 6) % 7; // Monday = 0
            var thursday = d.AddDays(3 - dow);
            var week = (thursday.DayOfYear - 1) / 7 + 1;
            return new IsoWeek(thursday.Year, week);
        }

        public DateTime Monday
        {
            get
            {
                var jan4 = new DateTime(Year, 1, 4);
                var dow = ((int)jan4.DayOfWeek + 6) % 7;
                return jan4.AddDays(-dow).AddDays((Week - 1) * 7);
            }
        }

        public IsoWeek AddWeeks(int weeks)
        {
            return FromDate(Monday.AddDays(7 * weeks));
        }

        public static int WeeksInYear(int year)
        {
            Func<int, int> p = y => (y + y / 4 - y / 100 + y / 400) % 7;
            return p(year) == 4 || p(year - 1) == 3 ? 53 : 52;
        }

        public static IsoWeek Parse(string text)
        {
            // 2019-W07
            if (text == null || text.Length != 8 || text[4] != '-' || text[5] != 'W')
            {
                throw new FormatException("Expected yyyy-Www: " + text);
            }

            return new IsoWeek(
                int.Parse(text.Substring(0, 4), CultureInfo.InvariantCulture),
                int.Parse(text.Substring(6, 2), CultureInfo.InvariantCulture));
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0000}-W{1:00}", Year, Week);
        }

        public bool Equals(IsoWeek other)
        {
            return Year == other.Year && Week == other.Week;
        }

        public override bool Equals(object obj)
        {
            return obj is IsoWeek && Equals((IsoWeek)obj);
        }

        public override int GetHashCode()
        {
            return Year * 100 + Week;
        }

        public int CompareTo(IsoWeek other)
        {
            return GetHashCode().CompareTo(other.GetHashCode());
        }
    }
}
