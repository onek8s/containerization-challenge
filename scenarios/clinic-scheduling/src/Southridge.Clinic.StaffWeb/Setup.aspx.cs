using System;
using System.Web.Security;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.StaffWeb
{
    /// <summary>Creates the first administrator on an empty membership database. Disabled once any user exists.</summary>
    public partial class SetupPage : ClinicPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (UsersExist())
            {
                SetupViews.SetActiveView(DoneView);
            }
        }

        protected void CreateButton_Click(object sender, EventArgs e)
        {
            if (!IsValid || UsersExist())
            {
                return;
            }

            MembershipCreateStatus status;
            Membership.CreateUser(UserNameBox.Text.Trim(), PasswordBox.Text, EmailBox.Text.Trim(), null, null, true, out status);
            if (status != MembershipCreateStatus.Success)
            {
                ShowError("The account could not be created: " + status + ".");
                return;
            }

            Roles.AddUserToRoles(UserNameBox.Text.Trim(), new[] { ClinicRoles.Administrator, ClinicRoles.Manager });
            StaffProfile profile = StaffProfile.For(UserNameBox.Text.Trim());
            profile.FullName = FullNameBox.Text.Trim();
            profile.DefaultSiteId = 1;
            profile.Save();
            ClinicLog.Audit(UserNameBox.Text.Trim(), "Setup", "First administrator created from " + Request.UserHostAddress);
            SetupViews.SetActiveView(DoneView);
        }

        private static bool UsersExist()
        {
            int total;
            Membership.GetAllUsers(0, 1, out total);
            return total > 0;
        }
    }
}
