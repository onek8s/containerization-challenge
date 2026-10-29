using System;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.StaffWeb.Controls
{
    /// <summary>Demographic fields shared by registration and the patient edit page.</summary>
    public partial class PatientFields : UserControl
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        public string ValidationGroup
        {
            get { return (string)ViewState["ValidationGroup"] ?? string.Empty; }
            set { ViewState["ValidationGroup"] = value; }
        }

        protected void Page_Init(object sender, EventArgs e)
        {
            SiteList.DataSource = StaffCache.Sites();
            SiteList.DataBind();
            ProviderList.DataSource = StaffCache.Providers(null);
            ProviderList.DataBind();
        }

        protected void Page_PreRender(object sender, EventArgs e)
        {
            foreach (Control c in Controls)
            {
                var validator = c as BaseValidator;
                if (validator != null)
                {
                    validator.ValidationGroup = ValidationGroup;
                }
            }

            Summary.ValidationGroup = ValidationGroup;
        }

        public string DisplayName
        {
            get { return LastNameBox.Text.Trim() + ", " + FirstNameBox.Text.Trim(); }
        }

        public void LoadPatient(Patient p)
        {
            FirstNameBox.Text = p.FirstName;
            MiddleNameBox.Text = p.MiddleName;
            LastNameBox.Text = p.LastName;
            DateOfBirthBox.Text = p.DateOfBirth.ToString("MM/dd/yyyy", Us);
            SexList.SelectedValue = p.Sex.ToString(CultureInfo.InvariantCulture);
            Address1Box.Text = p.Address1;
            Address2Box.Text = p.Address2;
            CityBox.Text = p.City;
            if (StateList.Items.FindByValue(p.State) == null)
            {
                StateList.Items.Add(p.State);
            }

            StateList.SelectedValue = p.State;
            ZipBox.Text = p.Zip;
            HomePhoneBox.Text = PhoneNumber.Format(p.HomePhone);
            MobilePhoneBox.Text = PhoneNumber.Format(p.MobilePhone);
            EmailBox.Text = p.Email;
            ContactList.SelectedValue = string.IsNullOrEmpty(p.PreferredContact) ? "Phone" : p.PreferredContact;
            ReminderBox.Checked = p.ReminderOptIn;
            SiteList.SelectedValue = p.PreferredSiteId.HasValue ? p.PreferredSiteId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            ProviderList.SelectedValue = p.PrimaryProviderId.HasValue ? p.PrimaryProviderId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        public void ApplyTo(Patient p)
        {
            p.FirstName = FirstNameBox.Text.Trim();
            p.MiddleName = Blank(MiddleNameBox.Text);
            p.LastName = LastNameBox.Text.Trim();
            p.DateOfBirth = DateOfBirth;
            p.Sex = byte.Parse(SexList.SelectedValue, CultureInfo.InvariantCulture);
            p.Address1 = Blank(Address1Box.Text);
            p.Address2 = Blank(Address2Box.Text);
            p.City = Blank(CityBox.Text);
            p.State = StateList.SelectedValue == "Other" ? null : StateList.SelectedValue;
            p.Zip = Blank(ZipBox.Text);
            p.HomePhone = Blank(HomePhoneBox.Text);
            p.MobilePhone = Blank(MobilePhoneBox.Text);
            p.Email = Blank(EmailBox.Text);
            p.PreferredContact = ContactList.SelectedValue;
            p.ReminderOptIn = ReminderBox.Checked;
            p.PreferredSiteId = Int(SiteList.SelectedValue);
            p.PrimaryProviderId = Int(ProviderList.SelectedValue);
        }

        public NewPatient ToNewPatient()
        {
            return new NewPatient
            {
                FirstName = FirstNameBox.Text.Trim(),
                MiddleName = Blank(MiddleNameBox.Text),
                LastName = LastNameBox.Text.Trim(),
                DateOfBirth = DateOfBirth,
                Sex = byte.Parse(SexList.SelectedValue, CultureInfo.InvariantCulture),
                Address1 = Blank(Address1Box.Text),
                Address2 = Blank(Address2Box.Text),
                City = Blank(CityBox.Text),
                State = StateList.SelectedValue == "Other" ? null : StateList.SelectedValue,
                Zip = Blank(ZipBox.Text),
                HomePhone = Blank(HomePhoneBox.Text),
                MobilePhone = Blank(MobilePhoneBox.Text),
                Email = Blank(EmailBox.Text),
                PreferredSiteId = Int(SiteList.SelectedValue)
            };
        }

        private DateTime DateOfBirth
        {
            get
            {
                DateTime value;
                if (!DateTime.TryParse(DateOfBirthBox.Text, Us, DateTimeStyles.None, out value))
                {
                    throw new ClinicRuleException("The date of birth is not valid.");
                }

                return value.Date;
            }
        }

        private static string Blank(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0 ? null : value.Trim();
        }

        private static int? Int(string value)
        {
            int result;
            return int.TryParse(value, out result) ? result : (int?)null;
        }
    }
}
