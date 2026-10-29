using System;
using System.Diagnostics;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Configuration;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Diagnostics;
using Northwind.Inspections.Infrastructure.Imaging;

namespace Northwind.Inspections.Jobs
{
    internal sealed class PurgeJob
    {
        public int Run()
        {
            var settings = InspectionSettings.Current;
            var db = new Db(settings.ConnectionString);
            var inspections = new InspectionRepository(db);
            var photos = new PhotoRepository(db);
            var store = new PhotoStore(settings, db);
            var now = DateTime.UtcNow;
            var purgedInspections = 0;
            var purgedPhotos = 0;

            foreach (var inspection in inspections.PurgeCandidates())
            {
                if (!ReportSchedule.IsPurgeDue(inspection, now, settings.PurgeAfterDays))
                {
                    continue;
                }
                foreach (var photo in photos.ForInspection(inspection.Id))
                {
                    if (photo.Purged)
                    {
                        continue;
                    }
                    store.Remove(photo);
                    photos.MarkPurged(photo.Id);
                    purgedPhotos++;
                }
                inspections.Audit(inspection.Id, "Photos purged", "Inspections.Jobs", settings.PurgeAfterDays + " days retention");
                purgedInspections++;
            }

            InspectionsLog.Info(EventId.PurgeCompleted, "Purge removed {0} photos of {1} inspections (retention {2} days).", purgedPhotos, purgedInspections, settings.PurgeAfterDays);
            Trace.TraceInformation("purge: {0} photos of {1} inspections removed.", purgedPhotos, purgedInspections);
            return 0;
        }
    }
}
