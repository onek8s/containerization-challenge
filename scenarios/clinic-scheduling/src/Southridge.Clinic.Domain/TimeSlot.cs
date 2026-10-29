using System;
using System.Globalization;

namespace Southridge.Clinic.Domain
{
    /// <summary>A bookable period for one provider at one site. Times are clinic local time.</summary>
    [Serializable]
    public struct TimeSlot : IEquatable<TimeSlot>
    {
        private readonly DateTime _start;
        private readonly DateTime _end;

        public TimeSlot(DateTime start, DateTime end)
        {
            if (end <= start)
            {
                throw new ArgumentException("A slot must end after it starts.");
            }

            _start = start;
            _end = end;
        }

        public DateTime Start
        {
            get { return _start; }
        }

        public DateTime End
        {
            get { return _end; }
        }

        public int Minutes
        {
            get { return (int)(_end - _start).TotalMinutes; }
        }

        public bool Overlaps(DateTime start, DateTime end)
        {
            return start < _end && end > _start;
        }

        public bool Overlaps(TimeSlot other)
        {
            return Overlaps(other.Start, other.End);
        }

        public bool Contains(DateTime start, DateTime end)
        {
            return start >= _start && end <= _end;
        }

        public bool Equals(TimeSlot other)
        {
            return _start == other._start && _end == other._end;
        }

        public override bool Equals(object obj)
        {
            return obj is TimeSlot && Equals((TimeSlot)obj);
        }

        public override int GetHashCode()
        {
            return _start.GetHashCode() ^ (_end.GetHashCode() << 1);
        }

        public override string ToString()
        {
            return _start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "-" + _end.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
