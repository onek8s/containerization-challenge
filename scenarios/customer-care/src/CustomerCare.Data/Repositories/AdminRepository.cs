using System;
using System.Collections.Generic;
using System.Linq;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Data.Repositories
{
    /// <summary>Maintenance of the reference data. Every change clears the in-memory copy in <see cref="ReferenceData"/>.</summary>
    public class AdminRepository
    {
        public IList<Queue> Queues()
        {
            using (var db = new CareDbContext())
            {
                return db.Queues.AsNoTracking().OrderBy(q => q.SortOrder).ThenBy(q => q.Name).ToList();
            }
        }

        public void SaveQueue(Queue queue)
        {
            using (var db = new CareDbContext())
            {
                if (queue.Id == 0)
                {
                    queue.Code = queue.Code.Trim().ToUpperInvariant();
                    if (db.Queues.Any(q => q.Code == queue.Code)) throw new ArgumentException("Queue code " + queue.Code + " already exists.");
                    db.Queues.Add(queue);
                }
                else
                {
                    var existing = db.Queues.Find(queue.Id);
                    existing.Name = queue.Name;
                    existing.Email = queue.Email;
                    existing.IsActive = queue.IsActive;
                    existing.SortOrder = queue.SortOrder;
                    existing.DefaultPriority = queue.DefaultPriority;
                }

                db.SaveChanges();
            }

            ReferenceData.Invalidate();
        }

        public IList<RoutingRule> Rules()
        {
            using (var db = new CareDbContext())
            {
                return db.RoutingRules.AsNoTracking().OrderBy(r => r.Sequence).ToList();
            }
        }

        public void AddRule(RoutingRule rule)
        {
            if (ReferenceData.QueueByCode(rule.QueueCode) == null) throw new ArgumentException("Unknown queue " + rule.QueueCode + ".");
            using (var db = new CareDbContext())
            {
                db.RoutingRules.Add(rule);
                db.SaveChanges();
            }

            ReferenceData.Invalidate();
        }

        public void DeleteRule(int id)
        {
            using (var db = new CareDbContext())
            {
                var r = db.RoutingRules.Find(id);
                if (r != null)
                {
                    db.RoutingRules.Remove(r);
                    db.SaveChanges();
                }
            }

            ReferenceData.Invalidate();
        }

        public IList<SlaTarget> SlaTargets()
        {
            using (var db = new CareDbContext())
            {
                return db.SlaTargets.AsNoTracking().OrderByDescending(s => s.Priority).ToList();
            }
        }

        public void SaveSlaTargets(IEnumerable<SlaTarget> targets)
        {
            using (var db = new CareDbContext())
            {
                foreach (var t in targets)
                {
                    if (t.ResponseHours <= 0 || t.ResolveHours < t.ResponseHours) throw new ArgumentException("Targets for " + t.Priority + " are not valid.");
                    var existing = db.SlaTargets.Find(t.Priority);
                    if (existing == null)
                    {
                        db.SlaTargets.Add(t);
                    }
                    else
                    {
                        existing.ResponseHours = t.ResponseHours;
                        existing.ResolveHours = t.ResolveHours;
                    }
                }

                db.SaveChanges();
            }

            ReferenceData.Invalidate();
        }

        public IList<Holiday> Holidays()
        {
            using (var db = new CareDbContext())
            {
                return db.Holidays.AsNoTracking().OrderBy(h => h.Date).ToList();
            }
        }

        public void AddHoliday(DateTime date, string name)
        {
            using (var db = new CareDbContext())
            {
                if (db.Holidays.Find(date.Date) != null) throw new ArgumentException(date.ToString("d") + " is already a holiday.");
                db.Holidays.Add(new Holiday { Date = date.Date, Name = name });
                db.SaveChanges();
            }

            ReferenceData.Invalidate();
        }

        public void DeleteHoliday(DateTime date)
        {
            using (var db = new CareDbContext())
            {
                var h = db.Holidays.Find(date.Date);
                if (h != null)
                {
                    db.Holidays.Remove(h);
                    db.SaveChanges();
                }
            }

            ReferenceData.Invalidate();
        }
    }
}
