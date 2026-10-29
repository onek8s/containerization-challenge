using System;
using System.Linq;
using System.Web.Security;
using System.Web.UI.WebControls;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.StaffWeb.Admin
{
    public partial class UsersPage : ClinicPage
    {
        private string EditingUser
        {
            get { return (string)ViewState["EditingUser"]; }
            set { ViewState["EditingUser"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                RoleFilter.DataSource = ClinicRoles.StaffRoles;
                RoleFilter.DataBind();
                RoleList.DataSource = ClinicRoles.StaffRoles.Concat(new[] { ClinicRoles.ServiceAccount }).ToArray();
                RoleList.DataBind();
                SiteList.DataSource = StaffCache.Sites();
                SiteList.DataBind();
                ProviderList.DataSource = StaffCache.Providers(null);
                ProviderList.DataBind();
            }
        }

        protected void RoleFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            UserGrid.PageIndex = 0;
            UserGrid.DataBind();
        }

        protected void UserGrid_SelectedIndexChanged(object sender, EventArgs e)
        {
            string userName = (string)UserGrid.SelectedDataKey.Value;
            MembershipUser user = Membership.GetUser(userName);
            if (user == null)
            {
                return;
            }

            StaffProfile profile = StaffProfile.For(userName);
            EditingUser = userName;
            EditTitle.Text = "Edit " + Server.HtmlEncode(userName);
            UserNameBox.Text = userName;
            UserNameBox.ReadOnly = true;
            FullNameBox.Text = profile.FullName;
            EmailBox.Text = user.Email;
            ActiveBox.Checked = user.IsApproved;
            SelectValue(SiteList, profile.DefaultSiteId.ToString());
            SelectValue(ProviderList, profile.ProviderId.HasValue ? profile.ProviderId.Value.ToString() : string.Empty);
            string[] roles = Roles.GetRolesForUser(userName);
            foreach (ListItem item in RoleList.Items)
            {
                item.Selected = roles.Contains(item.Value);
            }

            SaveButton.Text = "Save";
            UnlockButton.Visible = user.IsLockedOut;
            ResetButton.Visible = true;
            PasswordLabel.Text = string.Empty;
        }

        protected void SaveButton_Click(object sender, EventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            string userName = EditingUser ?? UserNameBox.Text.Trim();
            try
            {
                MembershipUser user;
                if (EditingUser == null)
                {
                    string password = Membership.GeneratePassword(12, 2);
                    MembershipCreateStatus status;
                    user = Membership.CreateUser(userName, password, EmailBox.Text.Trim(), null, null, ActiveBox.Checked, out status);
                    if (status != MembershipCreateStatus.Success)
                    {
                        ShowError("The user could not be created: " + status);
                        return;
                    }

                    PasswordLabel.Text = "Temporary password: " + Server.HtmlEncode(password);
                }
                else
                {
                    user = Membership.GetUser(userName);
                    user.Email = EmailBox.Text.Trim();
                    user.IsApproved = ActiveBox.Checked;
                    Membership.UpdateUser(user);
                }

                if (userName == UserName && !RoleList.Items.FindByValue(ClinicRoles.Administrator).Selected)
                {
                    ShowError("You cannot remove your own administrator role.");
                    return;
                }

                string[] current = Roles.GetRolesForUser(userName);
                string[] wanted = RoleList.Items.Cast<ListItem>().Where(i => i.Selected).Select(i => i.Value).ToArray();
                string[] remove = current.Except(wanted).ToArray();
                string[] add = wanted.Except(current).ToArray();
                if (remove.Length > 0)
                {
                    Roles.RemoveUserFromRoles(userName, remove);
                }

                if (add.Length > 0)
                {
                    Roles.AddUserToRoles(userName, add);
                }

                StaffProfile profile = StaffProfile.For(userName);
                profile.FullName = FullNameBox.Text.Trim();
                profile.DefaultSiteId = int.Parse(SiteList.SelectedValue);
                profile.ProviderId = ProviderList.SelectedValue.Length > 0 ? int.Parse(ProviderList.SelectedValue) : (int?)null;
                profile.Save();

                Audit(EditingUser == null ? "CreateUser" : "EditUser", "User", userName, null, string.Join(",", wanted));
                UserGrid.DataBind();
                ShowMessage("Saved " + userName + ".");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected void UnlockButton_Click(object sender, EventArgs e)
        {
            MembershipUser user = Membership.GetUser(EditingUser);
            if (user != null && user.UnlockUser())
            {
                Audit("UnlockUser", "User", EditingUser, null, null);
                UnlockButton.Visible = false;
                UserGrid.DataBind();
                ShowMessage(EditingUser + " unlocked.");
            }
        }

        protected void ResetButton_Click(object sender, EventArgs e)
        {
            MembershipUser user = Membership.GetUser(EditingUser);
            if (user == null)
            {
                return;
            }

            if (user.IsLockedOut)
            {
                user.UnlockUser();
            }

            string password = user.ResetPassword();
            Audit("ResetPassword", "User", EditingUser, null, null);
            PasswordLabel.Text = "Temporary password: " + Server.HtmlEncode(password);
        }

        protected void ClearButton_Click(object sender, EventArgs e)
        {
            EditingUser = null;
            EditTitle.Text = "New user";
            UserNameBox.ReadOnly = false;
            UserNameBox.Text = FullNameBox.Text = EmailBox.Text = PasswordLabel.Text = string.Empty;
            ActiveBox.Checked = true;
            RoleList.ClearSelection();
            SaveButton.Text = "Create user";
            UnlockButton.Visible = ResetButton.Visible = false;
            UserGrid.SelectedIndex = -1;
        }

        private static void SelectValue(ListControl list, string value)
        {
            ListItem item = list.Items.FindByValue(value);
            list.ClearSelection();
            if (item != null)
            {
                item.Selected = true;
            }
        }
    }
}
