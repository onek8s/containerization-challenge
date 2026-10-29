using System;
using System.Collections.Generic;
using System.ComponentModel;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;

namespace Southridge.Clinic.StaffWeb.DataSources
{
    /// <summary>ObjectDataSource for the schedule day view.</summary>
    [DataObject]
    public class AppointmentDataSource
    {
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public List<DayAppointment> Day(int siteId, DateTime date, int? providerId, bool includeCancelled)
        {
            return new AppointmentRepository().ListByDay(siteId, date, providerId, includeCancelled);
        }

        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public IList<WaitlistCandidate> Waitlist(int providerId, int siteId, DateTime date)
        {
            return new AppointmentRepository().WaitlistCandidates(providerId, siteId, date, null);
        }
    }
}
