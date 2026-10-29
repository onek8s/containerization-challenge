using System;
using System.Globalization;
using System.Web.Security;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb.Account
{
    /// <summary>Connects a new online account to an existing patient record, or registers a new patient.</summary>
    public partial class LinkPage : PortalPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && Patient != null)
            {
                Done();
            }
        }

        protected void ExistingButton_Click(object sender, EventArgs e)
        {
            LinkViews.SetActiveView(ExistingView);
        }

        protected void NewButton_Click(object sender, EventArgs e)
        {
            TryBooking(delegate
            {
                SiteList.DataSource = Booking.GetSites();
                SiteList.DataBind();
            });
            LinkViews.SetActiveView(NewView);
        }

        protected void LinkButton_Click(object sender, EventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            DateTime birth = DateTime.Parse(LinkBirthBox.Text, Us);
            int patientId = 0;
            if (!TryBooking(() => patientId = Booking.LinkAccount(MrnBox.Text.Trim(), LinkLastNameBox.Text.Trim(), birth, User.Identity.Name)))
            {
                return;
            }

            if (patientId == 0)
            {
                ShowMessage("We could not find a record with those details. Check them against a statement, or call the clinic.", true);
                return;
            }

            PatientContext.Refresh();
            Done();
        }

        protected void RegisterButton_Click(object sender, EventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            MembershipUser account = Membership.GetUser();
            RegistrationResponse response = null;
            bool called = TryBooking(() => response = Booking.RegisterPatient(new PatientRegistration
            {
                FirstName = FirstNameBox.Text.Trim(),
                LastName = LastNameBox.Text.Trim(),
                DateOfBirth = DateTime.Parse(BirthBox.Text, Us),
                Sex = SexList.SelectedValue,
                Address1 = AddressBox.Text.Trim(),
                City = CityBox.Text.Trim(),
                State = StateBox.Text.Trim().ToUpperInvariant(),
                Zip = ZipBox.Text.Trim(),
                MobilePhone = PhoneBox.Text.Trim(),
                Email = account == null ? null : account.Email,
                PreferredSiteId = SiteList.SelectedValue.Length > 0 ? int.Parse(SiteList.SelectedValue, CultureInfo.InvariantCulture) : (int?)null,
                PortalUserName = User.Identity.Name
            }));
            if (!called)
            {
                return;
            }

            if (!response.Success)
            {
                ShowMessage(response.Message, true);
                return;
            }

            PatientContext.Refresh();
            Done();
        }

        private void Done()
        {
            string returnUrl = Request.QueryString["ReturnUrl"];
            bool local = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/", StringComparison.Ordinal) && !returnUrl.StartsWith("//", StringComparison.Ordinal);
            Response.Redirect(local ? returnUrl : "~/MyHealth/Appointments", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
