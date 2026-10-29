using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb.Book
{
    /// <summary>Online booking (2009): visit type and clinic, then a time from the booking service, then confirm.</summary>
    public partial class BookPage : PortalPage
    {
        private const int DaysPerSearch = 7;
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        protected void Page_Load(object sender, EventArgs e)
        {
            RequireLinkedPatient();
            if (!IsPostBack)
            {
                TryBooking(delegate
                {
                    TypeList.DataSource = Booking.GetAppointmentTypes();
                    TypeList.DataBind();
                    SiteList.DataSource = Booking.GetSites();
                    SiteList.DataBind();
                });

                for (int week = 0; week < 8; week++)
                {
                    DateTime from = DateTime.Today.AddDays(week * DaysPerSearch);
                    WeekList.Items.Add(new ListItem(week == 0 ? "This week" : "Week of " + from.ToString("MMMM d", Us), from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                }
            }
        }

        protected void BookWizard_NextButtonClick(object sender, WizardNavigationEventArgs e)
        {
            if (e.CurrentStepIndex == 0)
            {
                e.Cancel = !LoadSlots();
            }
            else if (e.CurrentStepIndex == 1)
            {
                if (SlotList.SelectedIndex < 0)
                {
                    ShowMessage("Choose a time first.", true);
                    e.Cancel = true;
                    return;
                }

                ConfirmText.Text = TypeList.SelectedItem.Text + ", " + SlotList.SelectedItem.Text;
            }
        }

        protected void BookWizard_FinishButtonClick(object sender, WizardNavigationEventArgs e)
        {
            string[] parts = SlotList.SelectedValue.Split('|');
            int providerId = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int siteId = int.Parse(parts[1], CultureInfo.InvariantCulture);
            DateTime start = DateTime.ParseExact(parts[2], "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            BookingResponse response = null;
            if (!TryBooking(() => response = Booking.Book(Patient.PatientId, int.Parse(TypeList.SelectedValue, CultureInfo.InvariantCulture),
                providerId, siteId, start, ReasonBox.Text.Trim(), User.Identity.Name)))
            {
                e.Cancel = true;
                return;
            }

            if (!response.Success)
            {
                ShowMessage(response.Message + " Please choose another time.", true);
                BookWizard.ActiveStepIndex = 1;
                LoadSlots();
                e.Cancel = true;
                return;
            }

            DoneText.Text = ConfirmText.Text;
            CodeText.Text = Server.HtmlEncode(response.ConfirmationCode);
            IntakeLink.NavigateUrl = "~/MyHealth/Forms/Intake?appointmentId=" + response.AppointmentId;
        }

        private bool LoadSlots()
        {
            DateTime from = DateTime.ParseExact(WeekList.SelectedValue, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            SlotItem[] slots = null;
            if (!TryBooking(() => slots = Booking.FindSlots(int.Parse(TypeList.SelectedValue, CultureInfo.InvariantCulture), from, DaysPerSearch,
                int.Parse(SiteList.SelectedValue, CultureInfo.InvariantCulture), 0)))
            {
                return false;
            }

            SlotList.Items.Clear();
            foreach (SlotItem slot in slots.OrderBy(s => s.Start).Take(60))
            {
                string text = slot.Start.ToString("ddd MMM d, h:mm tt", Us) + " with " + slot.ProviderName + " at " + slot.SiteName;
                string value = slot.ProviderId.ToString(CultureInfo.InvariantCulture) + "|" + slot.SiteId.ToString(CultureInfo.InvariantCulture) + "|"
                    + slot.Start.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
                SlotList.Items.Add(new ListItem(Server.HtmlEncode(text), value));
            }

            SlotSummary.Text = Server.HtmlEncode(TypeList.SelectedItem.Text + ", " + WeekList.SelectedItem.Text.ToLowerInvariant());
            NoSlotsLabel.Visible = SlotList.Items.Count == 0;
            return true;
        }
    }
}
