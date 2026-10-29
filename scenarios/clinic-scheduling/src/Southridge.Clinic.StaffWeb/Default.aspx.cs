using System;
using System.Collections.Generic;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Referrals;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb
{
    public partial class DefaultPage : ClinicPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                Site site = ClinicLookups.Site(CurrentSiteId);
                SiteName.Text = site == null ? "the clinic" : Server.HtmlEncode(site.Name);
                Bind();
            }
        }

        protected void RefreshTimer_Tick(object sender, EventArgs e)
        {
            Bind();
        }

        private void Bind()
        {
            try
            {
                List<DayAppointment> day = new AppointmentRepository().ListByDay(CurrentSiteId, DateTime.Today, null, false);
                BookedCount.Text = day.Count.ToString();
                WaitingCount.Text = day.Count(a => a.Status == (byte)AppointmentStatus.CheckedIn).ToString();
                InRoomCount.Text = day.Count(a => a.Status == (byte)AppointmentStatus.InRoom).ToString();
                CompletedCount.Text = day.Count(a => a.Status == (byte)AppointmentStatus.Completed).ToString();
                NoShowCount.Text = day.Count(a => a.Status == (byte)AppointmentStatus.NoShow).ToString();
                WaitingGrid.DataSource = day.Where(a => a.Status == (byte)AppointmentStatus.CheckedIn).OrderBy(a => a.CheckedInOn).ToList();
                WaitingGrid.DataBind();

                int openReferrals = ReferralSessions.InUnitOfWork(s => new ReferralRepository(s).Queue(null).Count);
                ReferralLink.Text = openReferrals + " referrals waiting for a decision or scheduling";
                ReferralLink.Visible = IsInAnyRole("ReferralCoordinator", "PracticeManager", "Administrator");
                UnbilledLink.Text = StaffCache.UnbilledEncounters() + " visits older than 3 days without charges";
                UnbilledLink.Visible = IsInAnyRole("Billing", "PracticeManager", "Administrator");
                int errors = new InterfaceRepository().Search(null, MessageStatus.Error, DateTime.Now.AddDays(-1), DateTime.Now, 500).Count;
                InterfaceLink.Text = errors + " interface messages in error (24 h)";
                InterfaceLink.Visible = User.IsInRole("Administrator");
                UpdatedAt.Text = DateTime.Now.ToString("t");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }
    }
}
