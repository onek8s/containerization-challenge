using System;
using System.Collections.Generic;
using System.Linq;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Scheduling
{
    /// <summary>One row of a provider's weekly template, resolved for a date.</summary>
    public class TemplateWindow
    {
        public int ProviderId { get; set; }
        public int SiteId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int SlotMinutes { get; set; }
        public string[] AppointmentTypes { get; set; }
        public int MaxOverbook { get; set; }

        public bool Allows(string appointmentTypeCode)
        {
            return AppointmentTypes == null || AppointmentTypes.Length == 0
                || AppointmentTypes.Contains(appointmentTypeCode, StringComparer.OrdinalIgnoreCase);
        }
    }

    public class BusyPeriod
    {
        public int? ProviderId { get; set; }
        public int? SiteId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool IsBlock { get; set; }
    }

    [Serializable]
    public class AvailableSlot
    {
        public int ProviderId { get; set; }
        public int SiteId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string ProviderName { get; set; }
        public string SiteName { get; set; }

        public string Key
        {
            get { return ProviderId + "|" + SiteId + "|" + Start.ToString("yyyyMMddHHmm"); }
        }
    }

    /// <summary>
    /// Works out free appointment times from provider templates, blocks (vacation, meetings, site closures)
    /// and booked appointments. Pure logic; <see cref="AvailabilityService"/> loads the inputs.
    /// </summary>
    public class SlotEngine
    {
        private readonly ClinicCalendar _calendar;

        public SlotEngine(ClinicCalendar calendar)
        {
            _calendar = calendar;
        }

        public IList<AvailableSlot> FindSlots(DateTime date, string appointmentTypeCode, int durationMinutes, IEnumerable<TemplateWindow> windows,
            IEnumerable<BusyPeriod> busy, DateTime earliestStart)
        {
            var result = new List<AvailableSlot>();
            if (_calendar.IsClosed(date))
            {
                return result;
            }

            List<BusyPeriod> busyList = busy.ToList();
            foreach (TemplateWindow window in windows.OrderBy(w => w.Start))
            {
                if (window.Start.Date != date.Date || !window.Allows(appointmentTypeCode))
                {
                    continue;
                }

                int step = window.SlotMinutes > 0 ? window.SlotMinutes : 15;
                for (DateTime start = window.Start; start.AddMinutes(durationMinutes) <= window.End; start = start.AddMinutes(step))
                {
                    DateTime end = start.AddMinutes(durationMinutes);
                    if (start < earliestStart)
                    {
                        continue;
                    }

                    bool taken = busyList.Any(b => b.Start < end && b.End > start
                        && (b.ProviderId == window.ProviderId || (b.IsBlock && b.ProviderId == null && b.SiteId == window.SiteId)));
                    if (!taken)
                    {
                        result.Add(new AvailableSlot { ProviderId = window.ProviderId, SiteId = window.SiteId, Start = start, End = end });
                    }
                }
            }

            return result;
        }

        /// <summary>First free slot per provider and day, used for the "next available" column.</summary>
        public static IList<AvailableSlot> FirstPerProviderAndDay(IEnumerable<AvailableSlot> slots)
        {
            return slots.GroupBy(s => new { s.ProviderId, s.Start.Date })
                .Select(g => g.OrderBy(s => s.Start).First())
                .OrderBy(s => s.Start)
                .ToList();
        }

        /// <summary>Resolves weekly template rows (time of day) to concrete windows for a date.</summary>
        public static TemplateWindow Resolve(DateTime date, int providerId, int siteId, TimeSpan startTime, TimeSpan endTime, int slotMinutes,
            string appointmentTypes, int maxOverbook)
        {
            return new TemplateWindow
            {
                ProviderId = providerId,
                SiteId = siteId,
                Start = date.Date + startTime,
                End = date.Date + endTime,
                SlotMinutes = slotMinutes,
                AppointmentTypes = string.IsNullOrEmpty(appointmentTypes)
                    ? new string[0]
                    : appointmentTypes.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray(),
                MaxOverbook = maxOverbook
            };
        }
    }
}
