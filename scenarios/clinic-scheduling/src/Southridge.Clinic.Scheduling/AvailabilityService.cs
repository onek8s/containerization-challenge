using System;
using System.Collections.Generic;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Scheduling
{
    public class SlotQuery
    {
        public DateTime FromDate { get; set; }
        public int Days { get; set; }
        public int AppointmentTypeId { get; set; }
        public int? SiteId { get; set; }
        public int? ProviderId { get; set; }
        public bool OnlineOnly { get; set; }
        public int MaxResults { get; set; }
    }

    /// <summary>Loads templates, blocks and booked appointments with LINQ to SQL and runs the slot engine.</summary>
    public class AvailabilityService
    {
        private readonly SlotEngine _engine;

        public AvailabilityService()
            : this(new SlotEngine(new ClinicCalendar()))
        {
        }

        public AvailabilityService(SlotEngine engine)
        {
            _engine = engine;
        }

        public IList<AvailableSlot> FindSlots(SlotQuery query, DateTime now)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                AppointmentType type = db.AppointmentTypes.Single(t => t.AppointmentTypeId == query.AppointmentTypeId);
                DateTime firstDay = query.FromDate.Date;
                DateTime lastDay = firstDay.AddDays(Math.Max(1, query.Days));

                IQueryable<Provider> providers = db.Providers.Where(p => p.IsActive);
                if (query.ProviderId.HasValue)
                {
                    providers = providers.Where(p => p.ProviderId == query.ProviderId.Value);
                }

                if (query.OnlineOnly)
                {
                    providers = providers.Where(p => p.OnlineBooking);
                }

                var templates = (from t in db.ScheduleTemplates
                                 join p in providers on t.ProviderId equals p.ProviderId
                                 where t.EffectiveFrom <= lastDay && (t.EffectiveTo == null || t.EffectiveTo >= firstDay)
                                       && (query.SiteId == null || t.SiteId == query.SiteId)
                                       && (!query.OnlineOnly || t.Site.OnlineBooking)
                                 select new
                                 {
                                     t.ProviderId,
                                     t.SiteId,
                                     t.DayOfWeek,
                                     t.StartTime,
                                     t.EndTime,
                                     t.SlotMinutes,
                                     t.AppointmentTypes,
                                     t.MaxOverbook,
                                     t.EffectiveFrom,
                                     t.EffectiveTo,
                                     ProviderName = p.LastName + ", " + p.FirstName + " " + p.Credentials,
                                     SiteName = t.Site.Name
                                 }).ToList();

                if (templates.Count == 0)
                {
                    return new List<AvailableSlot>();
                }

                int[] providerIds = templates.Select(t => t.ProviderId).Distinct().ToArray();
                int[] siteIds = templates.Select(t => t.SiteId).Distinct().ToArray();
                byte[] active = new[] { (byte)AppointmentStatus.Scheduled, (byte)AppointmentStatus.Confirmed, (byte)AppointmentStatus.CheckedIn, (byte)AppointmentStatus.InRoom };

                var busy = new List<BusyPeriod>();
                busy.AddRange(db.Appointments
                    .Where(a => providerIds.Contains(a.ProviderId) && a.StartsAt < lastDay && a.EndsAt > firstDay && active.Contains(a.Status))
                    .Select(a => new BusyPeriod { ProviderId = a.ProviderId, SiteId = a.SiteId, Start = a.StartsAt, End = a.EndsAt }));
                busy.AddRange(db.ScheduleBlocks
                    .Where(b => ((b.ProviderId != null && providerIds.Contains(b.ProviderId.Value)) || (b.ProviderId == null && b.SiteId != null && siteIds.Contains(b.SiteId.Value)))
                                && b.StartsAt < lastDay && b.EndsAt > firstDay)
                    .Select(b => new BusyPeriod { ProviderId = b.ProviderId, SiteId = b.SiteId, Start = b.StartsAt, End = b.EndsAt, IsBlock = true }));

                DateTime earliest = query.OnlineOnly ? now.AddHours(AppointmentRules.OnlineBookingMinLeadHours) : now;
                var names = templates.GroupBy(t => t.ProviderId).ToDictionary(g => g.Key, g => g.First().ProviderName);
                var siteNames = templates.GroupBy(t => t.SiteId).ToDictionary(g => g.Key, g => g.First().SiteName);
                var slots = new List<AvailableSlot>();
                for (DateTime day = firstDay; day < lastDay; day = day.AddDays(1))
                {
                    DateTime d = day;
                    var windows = templates
                        .Where(t => t.DayOfWeek == (int)d.DayOfWeek && t.EffectiveFrom <= d && (t.EffectiveTo == null || t.EffectiveTo >= d))
                        .Select(t => SlotEngine.Resolve(d, t.ProviderId, t.SiteId, t.StartTime, t.EndTime, t.SlotMinutes, t.AppointmentTypes, t.MaxOverbook));
                    slots.AddRange(_engine.FindSlots(d, type.Code, type.DurationMinutes, windows, busy, earliest));
                    if (query.MaxResults > 0 && slots.Count >= query.MaxResults)
                    {
                        break;
                    }
                }

                foreach (AvailableSlot slot in slots)
                {
                    slot.ProviderName = names[slot.ProviderId];
                    slot.SiteName = siteNames[slot.SiteId];
                }

                IEnumerable<AvailableSlot> ordered = slots.OrderBy(s => s.Start).ThenBy(s => s.ProviderName);
                return query.MaxResults > 0 ? ordered.Take(query.MaxResults).ToList() : ordered.ToList();
            }
        }
    }
}
