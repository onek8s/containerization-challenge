using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Referrals;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb.Schedule
{
    public partial class BookPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                TypeList.DataSource = StaffCache.AppointmentTypes();
                TypeList.DataBind();
                BindProviders();
                FromBox.Text = DateTime.Today.ToString("MM/dd/yyyy", Us);
                OverbookPanel.Visible = IsInAnyRole(ClinicRoles.Manager, ClinicRoles.Provider, ClinicRoles.Administrator);

                ViewState["ReferralId"] = QueryInt("referralId");
                int? patientId = QueryInt("patientId");
                if (patientId.HasValue)
                {
                    SelectPatient(patientId.Value);
                }
            }
        }

        protected void AllSitesBox_CheckedChanged(object sender, EventArgs e)
        {
            BindProviders();
        }

        protected void FindButton_Click(object sender, EventArgs e)
        {
            try
            {
                DateTime from;
                if (!DateTime.TryParse(FromBox.Text, Us, DateTimeStyles.None, out from))
                {
                    from = DateTime.Today;
                }

                IList<AvailableSlot> slots = new AvailabilityService().FindSlots(new SlotQuery
                {
                    AppointmentTypeId = int.Parse(TypeList.SelectedValue),
                    FromDate = from,
                    Days = int.Parse(DaysList.SelectedValue),
                    SiteId = AllSitesBox.Checked ? (int?)null : CurrentSiteId,
                    ProviderId = string.IsNullOrEmpty(ProviderList.SelectedValue) ? (int?)null : int.Parse(ProviderList.SelectedValue),
                    MaxResults = 80
                }, DateTime.Now);
                SlotRepeater.DataSource = slots;
                SlotRepeater.DataBind();
                NoSlotsPanel.Visible = slots.Count == 0;
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected void SlotRepeater_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            string[] parts = ((string)e.CommandArgument).Split('|');
            Book(int.Parse(parts[0]), int.Parse(parts[1]), DateTime.Parse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), false);
        }

        protected void OverbookButton_Click(object sender, EventArgs e)
        {
            DateTime when;
            if (string.IsNullOrEmpty(ProviderList.SelectedValue) || !DateTime.TryParse(OverbookTime.Text, Us, DateTimeStyles.None, out when))
            {
                ShowError("Choose a provider and enter the date and time for the overbook.");
                return;
            }

            Book(int.Parse(ProviderList.SelectedValue), CurrentSiteId, when, true);
        }

        protected void WaitlistButton_Click(object sender, EventArgs e)
        {
            int patientId;
            if (!int.TryParse(PatientId.Value, out patientId))
            {
                ShowError("Choose a patient first.");
                return;
            }

            using (ClinicDataContext db = ClinicDataContextFactory.Create())
            {
                db.WaitlistEntries.InsertOnSubmit(new WaitlistEntry
                {
                    PatientId = patientId,
                    AppointmentTypeId = int.Parse(TypeList.SelectedValue),
                    ProviderId = string.IsNullOrEmpty(ProviderList.SelectedValue) ? (int?)null : int.Parse(ProviderList.SelectedValue),
                    SiteId = CurrentSiteId,
                    EarliestDate = DateTime.Today,
                    Priority = 0,
                    Notes = string.IsNullOrEmpty(ReasonBox.Text) ? null : ReasonBox.Text.Trim(),
                    Source = "Staff",
                    CreatedOn = DateTime.Now
                });
                db.SubmitChanges();
            }

            ShowMessage("The patient was added to the waitlist.");
            NoSlotsPanel.Visible = false;
        }

        protected void AnotherButton_Click(object sender, EventArgs e)
        {
            SlotRepeater.DataSource = null;
            SlotRepeater.DataBind();
            BookViews.SetActiveView(SearchView);
        }

        private void Book(int providerId, int siteId, DateTime start, bool overbook)
        {
            int patientId;
            if (!int.TryParse(PatientId.Value, out patientId))
            {
                ShowError("Choose a patient first.");
                return;
            }

            try
            {
                BookingResult result = new SchedulingService().Book(new BookingRequest
                {
                    PatientId = patientId,
                    ProviderId = providerId,
                    SiteId = siteId,
                    AppointmentTypeId = int.Parse(TypeList.SelectedValue),
                    StartsAt = start,
                    Reason = ReasonBox.Text.Trim(),
                    AllowOverbook = overbook,
                    ReferralId = (int?)ViewState["ReferralId"],
                    Source = AppointmentSource.Staff,
                    BookedBy = UserName
                });
                LinkReferral(result.AppointmentId);
                BookedWhen.Text = start.ToString("dddd MMMM d, h:mm tt", Us);
                BookedCode.Text = result.ConfirmationCode;
                LetterLink.NavigateUrl = "~/Handlers/Letter.ashx?id=" + result.AppointmentId;
                AppointmentLink.NavigateUrl = "~/Schedule/Appointment.aspx?id=" + result.AppointmentId;
                BookViews.SetActiveView(DoneView);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        /// <summary>An approved referral booked from the referral page is marked scheduled and its workflow moves on.</summary>
        private void LinkReferral(int appointmentId)
        {
            var referralId = (int?)ViewState["ReferralId"];
            if (!referralId.HasValue)
            {
                return;
            }

            bool linked = ReferralSessions.InUnitOfWork(session =>
            {
                Referral referral = session.Get<Referral>(referralId.Value);
                if (referral == null || referral.Status != ReferralStatus.Approved)
                {
                    return false;
                }

                new ReferralService(session, ClinicContainer.Resolve<IClock>()).LinkAppointment(referral, appointmentId, UserName);
                return true;
            });
            if (linked)
            {
                ReferralWorkflowClient.Scheduled(referralId.Value, appointmentId);
            }

            ViewState["ReferralId"] = null;
        }

        private void SelectPatient(int patientId)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Patient p = db.Patients.SingleOrDefault(x => x.PatientId == patientId);
                if (p != null)
                {
                    PatientId.Value = p.PatientId.ToString();
                    PatientBox.Text = p.LastName + ", " + p.FirstName + "  " + p.DateOfBirth.ToString("MM/dd/yyyy", Us) + "  " + p.Mrn;
                }
            }
        }

        private void BindProviders()
        {
            string selected = ProviderList.SelectedValue;
            ProviderList.Items.Clear();
            ProviderList.Items.Add(new ListItem("(any)", string.Empty));
            ProviderList.DataSource = StaffCache.Providers(AllSitesBox.Checked ? (int?)null : CurrentSiteId);
            ProviderList.DataBind();
            if (ProviderList.Items.FindByValue(selected) != null)
            {
                ProviderList.SelectedValue = selected;
            }
        }
    }
}
