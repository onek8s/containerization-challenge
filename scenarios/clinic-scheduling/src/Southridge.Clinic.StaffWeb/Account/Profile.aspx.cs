using System;
using System.Globalization;

namespace Southridge.Clinic.StaffWeb.Account
{
    public partial class ProfilePage : ClinicPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                StaffProfile profile = StaffProfile.Current;
                SiteList.DataSource = StaffCache.Sites();
                SiteList.DataBind();
                SpecialtyList.DataSource = StaffCache.Specialties();
                SpecialtyList.DataBind();
                FullNameBox.Text = profile.FullName;
                Select(SiteList, profile.DefaultSiteId.ToString(CultureInfo.InvariantCulture));
                Select(SpecialtyList, profile.ReferralSpecialty ?? string.Empty);
                Select(PageSizeList, profile.GridPageSize.ToString(CultureInfo.InvariantCulture));
                ShowCancelledBox.Checked = profile.ShowCancelled;
            }
        }

        protected void SaveButton_Click(object sender, EventArgs e)
        {
            StaffProfile profile = StaffProfile.Current;
            profile.FullName = FullNameBox.Text.Trim();
            profile.DefaultSiteId = int.Parse(SiteList.SelectedValue, CultureInfo.InvariantCulture);
            profile.ReferralSpecialty = SpecialtyList.SelectedValue.Length > 0 ? SpecialtyList.SelectedValue : null;
            profile.GridPageSize = int.Parse(PageSizeList.SelectedValue, CultureInfo.InvariantCulture);
            profile.ShowCancelled = ShowCancelledBox.Checked;
            profile.Save();
            CurrentSiteId = profile.DefaultSiteId;
            ShowMessage("Preferences saved.");
        }

        protected void PasswordChange_ChangedPassword(object sender, EventArgs e)
        {
            Audit("ChangePassword", "User", UserName, null, null);
        }

        private static void Select(System.Web.UI.WebControls.ListControl list, string value)
        {
            if (list.Items.FindByValue(value) != null)
            {
                list.SelectedValue = value;
            }
        }
    }
}
