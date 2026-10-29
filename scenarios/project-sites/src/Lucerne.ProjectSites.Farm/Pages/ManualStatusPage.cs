using System;
using System.Globalization;
using System.Web.UI.WebControls;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Domain;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.WebControls;

namespace Lucerne.ProjectSites.Farm.Pages
{
    /// <summary>_layouts/15/Lucerne.ProjectSites/ManualStatus.aspx: PM override of the calculated RAG (valid for 7 days).</summary>
    public class ManualStatusPage : LayoutsPageBase
    {
        protected DropDownList StatusList;
        protected TextBox CommentBox;
        protected Button SaveButton;
        protected Button ClearButton;
        protected Literal Current;

        protected override SPBasePermissions RightsRequired
        {
            get { return SPBasePermissions.ManageWeb; }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SaveButton.Click += Save;
            ClearButton.Click += Clear;
            if (IsPostBack)
            {
                return;
            }

            StatusList.Items.Add(new ListItem(RagStatus.Green.ToString()));
            StatusList.Items.Add(new ListItem(RagStatus.Amber.ToString()));
            StatusList.Items.Add(new ListItem(RagStatus.Red.ToString()));

            var web = SPContext.Current.Site.RootWeb;
            var current = web.AllProperties[StatusRollupService.ManualStatusProperty] as string;
            Current.Text = string.IsNullOrEmpty(current)
                ? "No override, the status is calculated."
                : SPHttpUtility.HtmlEncode("Override " + current + " set on " + web.AllProperties[StatusRollupService.ManualStatusDateProperty]);
        }

        private void Save(object sender, EventArgs e)
        {
            var web = SPContext.Current.Site.RootWeb;
            web.AllowUnsafeUpdates = true;
            web.AllProperties[StatusRollupService.ManualStatusProperty] = StatusList.SelectedValue;
            web.AllProperties[StatusRollupService.ManualStatusDateProperty] = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            web.AllProperties[StatusRollupService.ManualStatusCommentProperty] = CommentBox.Text + " (" + SPContext.Current.Web.CurrentUser.Name + ")";
            web.Update();
            SPUtility.Redirect(web.Url, SPRedirectFlags.Default, Context);
        }

        private void Clear(object sender, EventArgs e)
        {
            var web = SPContext.Current.Site.RootWeb;
            web.AllowUnsafeUpdates = true;
            web.AllProperties.Remove(StatusRollupService.ManualStatusProperty);
            web.DeleteProperty(StatusRollupService.ManualStatusProperty);
            web.Update();
            SPUtility.Redirect(web.Url, SPRedirectFlags.Default, Context);
        }
    }
}
